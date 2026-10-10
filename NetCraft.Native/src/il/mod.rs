//! 方法体 IL 的解析与重建
//! CLR 交来的 COR_ILMETHOD 是一块自描述字节 这里把它拆成指令序列
//! 改完再拼回去 重建时会重算所有分支目标 并把放不下的短分支换成长分支

mod opcodes;
pub mod wire;

pub use opcodes::{OpCode, DOUBLE_BYTE, SINGLE_BYTE};

use std::collections::HashMap;

//lookup 按编码查指令表 双字节指令的编码高字节固定是 0xFE
pub fn lookup(code: u16) -> Option<OpCode> {
    if code > 0xFF {
        DOUBLE_BYTE.get((code & 0xFF) as usize).copied().flatten()
    } else {
        SINGLE_BYTE.get(code as usize).copied().flatten()
    }
}

//操作数类型 与 opcode.def 里的写法一一对应
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum OperandType {
    InlineNone,
    ShortInlineI,
    InlineI,
    InlineI8,
    ShortInlineR,
    InlineR,
    InlineMethod,
    InlineField,
    InlineType,
    InlineString,
    InlineSig,
    InlineTok,
    ShortInlineVar,
    InlineVar,
    ShortInlineBrTarget,
    InlineBrTarget,
    InlineSwitch,
}

//Operand 一条指令携带的数据
#[derive(Clone, Debug, PartialEq)]
pub enum Operand {
    None,
    I32(i32),
    I64(i64),
    F32(f32),
    F64(f64),
    //局部变量或参数的槽位
    Var(u16),
    //元数据 token
    Token(u32),
    //分支目标在方法体内的绝对字节偏移 解析时已从相对量换算过来
    Branch(u32),
    //switch 的各个分支目标 同样是绝对偏移
    Switch(Vec<u32>),
}

//Instruction 一条指令
#[derive(Clone, Debug)]
pub struct Instruction {
    //解析阶段是它在原方法体里的偏移 重建后不再更新
    pub offset: u32,
    //单字节指令是首字节 双字节指令是 0xFE00 或上第二字节
    pub code: u16,
    pub operand: Operand,
}

//MethodBody 一个方法体
#[derive(Clone, Debug)]
pub struct MethodBody {
    pub max_stack: u16,
    pub local_var_sig_tok: u32,
    //局部变量是否要求零初始化 重建时必须原样带上 丢了会让 CLR 判方法非法
    pub init_locals: bool,
    pub instructions: Vec<Instruction>,
    //原方法体是不是 fat 头 重建时统一按 fat 写 便于将来放得下更多东西
    pub was_fat: bool,
}

const COR_ILMETHOD_TINY_FORMAT: u8 = 0x2;
const COR_ILMETHOD_FAT_FORMAT: u8 = 0x3;
const COR_ILMETHOD_FORMAT_MASK: u8 = 0x3;
//Flags 里的这一位表示 code 后面还有节 目前只有异常处理表会用到
const COR_ILMETHOD_MORE_SECTS: u8 = 0x8;
//Flags 里的这一位要求 JIT 把局部变量清零
const COR_ILMETHOD_INIT_LOCALS: u8 = 0x10;

//slice_at 取一段字节 越界时给出带偏移的错误
fn slice_at(code: &[u8], start: usize, count: usize, offset: u32) -> Result<&[u8], String> {
    code.get(start..start + count)
        .ok_or_else(|| format!("偏移 {offset} 处操作数只够 {count} 字节"))
}

impl MethodBody {
    //parse 从一个 COR_ILMETHOD 字节块解析出方法体
    //带异常处理表的方法目前直接拒绝 那块布局另有一套 等要用时再补
    pub fn parse(bytes: &[u8]) -> Result<MethodBody, String> {
        let first = *bytes.first().ok_or("方法体是空的")?;
        let format = first & COR_ILMETHOD_FORMAT_MASK;

        let (code_start, code_size, max_stack, local_var_sig_tok, init_locals, was_fat) =
            if format == COR_ILMETHOD_TINY_FORMAT {
                //tiny 头就一个字节 高六位是码长
                (1usize, (first >> 2) as usize, 8u16, 0u32, false, false)
            } else if format == COR_ILMETHOD_FAT_FORMAT {
                if bytes.len() < 12 {
                    return Err("fat 头不足十二字节".to_string());
                }
                if first & COR_ILMETHOD_MORE_SECTS != 0 {
                    return Err("方法带异常处理节 暂不支持".to_string());
                }
                let max_stack = u16::from_le_bytes([bytes[2], bytes[3]]);
                let code_size = u32::from_le_bytes([bytes[4], bytes[5], bytes[6], bytes[7]]) as usize;
                let local_var_sig_tok = u32::from_le_bytes([bytes[8], bytes[9], bytes[10], bytes[11]]);
                let init_locals = first & COR_ILMETHOD_INIT_LOCALS != 0;
                (12usize, code_size, max_stack, local_var_sig_tok, init_locals, true)
            } else {
                return Err(format!("方法头格式不认 首字节 0x{first:02X}"));
            };

        let code = bytes
            .get(code_start..code_start + code_size)
            .ok_or_else(|| format!("码长 {code_size} 超出字节块"))?;

        Ok(MethodBody {
            max_stack,
            local_var_sig_tok,
            init_locals,
            instructions: decode(code)?,
            was_fat,
        })
    }

    //encode 把当前指令序列拼回一个 fat 方法体
    //分支目标按指令新位置重算 短分支放不下会自动升级成长分支
    pub fn encode(&self) -> Vec<u8> {
        let offsets = self.layout();
        let code_size = offsets.last().map_or(0, |(offset, _, size)| offset + size);

        let mut map: HashMap<u32, u32> = HashMap::with_capacity(self.instructions.len());
        for (index, instruction) in self.instructions.iter().enumerate() {
            map.insert(instruction.offset, offsets[index].0);
        }

        let mut code = Vec::with_capacity(code_size as usize);
        for (index, instruction) in self.instructions.iter().enumerate() {
            let (position, code_word, size) = offsets[index];
            write_opcode(&mut code, code_word);

            match &instruction.operand {
                Operand::None => {}
                Operand::I32(value) => {
                    if size - (size_of_opcode(code_word)) == 1 {
                        code.push(*value as u8);
                    } else {
                        code.extend_from_slice(&value.to_le_bytes());
                    }
                }
                Operand::I64(value) => code.extend_from_slice(&value.to_le_bytes()),
                Operand::F32(value) => code.extend_from_slice(&value.to_le_bytes()),
                Operand::F64(value) => code.extend_from_slice(&value.to_le_bytes()),
                Operand::Var(value) => {
                    if size - size_of_opcode(code_word) == 1 {
                        code.push(*value as u8);
                    } else {
                        code.extend_from_slice(&value.to_le_bytes());
                    }
                }
                Operand::Token(value) => code.extend_from_slice(&value.to_le_bytes()),
                Operand::Branch(target) => {
                    //落在指令上的取新位置 落在方法末尾的取码长
                    let destination = *map.get(target).unwrap_or(&code_size);
                    let next = position + size;
                    let delta = destination as i64 - next as i64;
                    if size - size_of_opcode(code_word) == 1 {
                        code.push(delta as i8 as u8);
                    } else {
                        code.extend_from_slice(&(delta as i32).to_le_bytes());
                    }
                }
                Operand::Switch(targets) => {
                    code.extend_from_slice(&(targets.len() as u32).to_le_bytes());
                    //表尾位置 从 opcode 之后算起 漏掉 opcode 自身会让所有目标整体偏一格
                    let base = position + size_of_opcode(code_word) + 4 + targets.len() as u32 * 4;
                    for target in targets {
                        let destination = *map.get(target).unwrap_or(&code_size);
                        let delta = destination as i64 - base as i64;
                        code.extend_from_slice(&(delta as i32).to_le_bytes());
                    }
                }
            }

            //双字节 opcode 的 size 里已经含了前缀 这里只做个自检
            debug_assert_eq!(code.len() as u32, position + size, "指令长度算错了");
        }

        let mut result = Vec::with_capacity(12 + code.len());
        let header_size_in_dwords: u16 = 3;
        //低十二位放标志 高四位放头长 头长按 DWORD 数记
        let mut flags: u16 = COR_ILMETHOD_FAT_FORMAT as u16 | (header_size_in_dwords << 12);
        if self.init_locals {
            flags |= COR_ILMETHOD_INIT_LOCALS as u16;
        }
        result.extend_from_slice(&flags.to_le_bytes());
        result.extend_from_slice(&self.max_stack.to_le_bytes());
        result.extend_from_slice(&(code.len() as u32).to_le_bytes());
        result.extend_from_slice(&self.local_var_sig_tok.to_le_bytes());
        result.extend_from_slice(&code);
        result
    }

    //layout 迭代算出每条指令的最终位置与最终 opcode
    //短分支放不下就升级成长分支 升级会让后面的偏移变大 可能又让别的分支放不下 所以要循环到稳定
    fn layout(&self) -> Vec<(u32, u16, u32)> {
        //初始全部按原样 短分支保持短分支
        let mut codes: Vec<u16> = self.instructions.iter().map(|i| i.code).collect();

        loop {
            let mut result = Vec::with_capacity(self.instructions.len());
            let mut position = 0u32;
            for (index, instruction) in self.instructions.iter().enumerate() {
                let size = size_of(instruction, codes[index]);
                result.push((position, codes[index], size));
                position += size;
            }
            let code_size = position;

            //按当前排布找出所有放不下的短分支
            let mut upgraded = false;
            for (index, instruction) in self.instructions.iter().enumerate() {
                let (position, code_word, size) = result[index];
                let operand_size = size - size_of_opcode(code_word);
                let fits = |delta: i64| -> bool {
                    if operand_size == 1 {
                        delta >= i8::MIN as i64 && delta <= i8::MAX as i64
                    } else {
                        delta >= i32::MIN as i64 && delta <= i32::MAX as i64
                    }
                };

                match &instruction.operand {
                    Operand::Branch(target) => {
                        //目标的新位置要按当前的排布查
                        let destination = result
                            .iter()
                            .enumerate()
                            .find_map(|(other, (offset, _, _))| {
                                (self.instructions[other].offset == *target).then_some(*offset)
                            })
                            .unwrap_or(code_size);
                        let delta = destination as i64 - (position + size) as i64;
                        if !fits(delta) {
                            if let Some(long_code) = long_branch_of(code_word) {
                                codes[index] = long_code;
                                upgraded = true;
                            }
                        }
                    }
                    Operand::Switch(targets) => {
                        let base = position + size_of_opcode(code_word) + 4 + targets.len() as u32 * 4;
                        for target in targets {
                            let destination = result
                                .iter()
                                .enumerate()
                                .find_map(|(other, (offset, _, _))| {
                                    (self.instructions[other].offset == *target).then_some(*offset)
                                })
                                .unwrap_or(code_size);
                            if !fits(destination as i64 - base as i64) {
                                //switch 的分支本来就是四字节 没有可升级的余地
                                break;
                            }
                        }
                    }
                    _ => {}
                }
            }

            if !upgraded {
                return result;
            }
        }
    }
}

//size_of_opcode opcode 自身占几个字节
fn size_of_opcode(code: u16) -> u32 {
    if code > 0xFF {
        2
    } else {
        1
    }
}

//size_of 一条指令编码后占几个字节
fn size_of(instruction: &Instruction, code: u16) -> u32 {
    let operand = match lookup(code) {
        Some(info) => size_of_operand(info.operand, &instruction.operand),
        None => 0,
    };
    size_of_opcode(code) + operand
}

//size_of_operand 某类操作数编码后占几个字节
fn size_of_operand(kind: OperandType, operand: &Operand) -> u32 {
    match kind {
        OperandType::InlineNone => 0,
        OperandType::ShortInlineI => 1,
        OperandType::InlineI => 4,
        OperandType::InlineI8 => 8,
        OperandType::ShortInlineR => 4,
        OperandType::InlineR => 8,
        OperandType::InlineMethod
        | OperandType::InlineField
        | OperandType::InlineType
        | OperandType::InlineString
        | OperandType::InlineSig
        | OperandType::InlineTok => 4,
        OperandType::ShortInlineVar => 1,
        OperandType::InlineVar => 2,
        OperandType::ShortInlineBrTarget => 1,
        OperandType::InlineBrTarget => 4,
        OperandType::InlineSwitch => match operand {
            Operand::Switch(targets) => 4 + targets.len() as u32 * 4,
            _ => 4,
        },
    }
}

//write_opcode 写出 opcode 本身
fn write_opcode(output: &mut Vec<u8>, code: u16) {
    if code > 0xFF {
        output.push(((code >> 8) & 0xFF) as u8);
        output.push((code & 0xFF) as u8);
    } else {
        output.push(code as u8);
    }
}

//long_branch_of 把短分支指令换成长分支 名字去掉 .s 再查表
fn long_branch_of(code: u16) -> Option<u16> {
    let info = lookup(code)?;
    let short_name = info.name.strip_suffix(".s")?;
    find_by_name(short_name)
}

//find_by_name 按字符串名反查 opcode
fn find_by_name(name: &str) -> Option<u16> {
    for index in 0..256u16 {
        if let Some(info) = SINGLE_BYTE[index as usize] {
            if info.name == name {
                return Some(index);
            }
        }
        if let Some(info) = DOUBLE_BYTE[index as usize] {
            if info.name == name {
                return Some(0xFE00 | index);
            }
        }
    }
    None
}

//decode 把指令字节流拆成指令序列 分支目标在此时换算成绝对偏移
fn decode(code: &[u8]) -> Result<Vec<Instruction>, String> {
    let mut instructions = Vec::new();
    let mut position = 0usize;

    while position < code.len() {
        let offset = position as u32;
        let first = code[position];

        let (code_word, opcode_size) = if first == 0xFE {
            let second = *code
                .get(position + 1)
                .ok_or_else(|| format!("偏移 {offset} 处 0xFE 后面没有第二字节"))?;
            (0xFE00u16 | second as u16, 2usize)
        } else {
            (first as u16, 1usize)
        };

        let info = lookup(code_word).ok_or_else(|| format!("偏移 {offset} 处未知指令 0x{code_word:04X}"))?;
        let operand_start = position + opcode_size;
        let (operand, operand_size) = decode_operand(info.operand, code, operand_start, offset)?;

        instructions.push(Instruction {
            offset,
            code: code_word,
            operand,
        });
        position = operand_start + operand_size;
    }

    Ok(instructions)
}

//decode_operand 读一个操作数 返回它和它的字节数
fn decode_operand(
    kind: OperandType,
    code: &[u8],
    start: usize,
    offset: u32,
) -> Result<(Operand, usize), String> {
    Ok(match kind {
        OperandType::InlineNone => (Operand::None, 0),
        OperandType::ShortInlineI => (Operand::I32(slice_at(code, start, 1, offset)?[0] as i8 as i32), 1),
        OperandType::InlineI => {
            let bytes = slice_at(code, start, 4, offset)?;
            (Operand::I32(i32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]])), 4)
        }
        OperandType::InlineI8 => {
            let bytes = slice_at(code, start, 8, offset)?;
            let mut raw = [0u8; 8];
            raw.copy_from_slice(bytes);
            (Operand::I64(i64::from_le_bytes(raw)), 8)
        }
        OperandType::ShortInlineR => {
            let bytes = slice_at(code, start, 4, offset)?;
            (Operand::F32(f32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]])), 4)
        }
        OperandType::InlineR => {
            let bytes = slice_at(code, start, 8, offset)?;
            let mut raw = [0u8; 8];
            raw.copy_from_slice(bytes);
            (Operand::F64(f64::from_le_bytes(raw)), 8)
        }
        OperandType::InlineMethod
        | OperandType::InlineField
        | OperandType::InlineType
        | OperandType::InlineString
        | OperandType::InlineSig
        | OperandType::InlineTok => {
            let bytes = slice_at(code, start, 4, offset)?;
            (Operand::Token(u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]])), 4)
        }
        OperandType::ShortInlineVar => (Operand::Var(slice_at(code, start, 1, offset)?[0] as u16), 1),
        OperandType::InlineVar => {
            let bytes = slice_at(code, start, 2, offset)?;
            (Operand::Var(u16::from_le_bytes([bytes[0], bytes[1]])), 2)
        }
        OperandType::ShortInlineBrTarget => {
            //相对下一条指令的偏移 这里换算成方法体内的绝对偏移
            let delta = slice_at(code, start, 1, offset)?[0] as i8 as i64;
            let next = start as i64 + 1;
            (Operand::Branch((next + delta) as u32), 1)
        }
        OperandType::InlineBrTarget => {
            let bytes = slice_at(code, start, 4, offset)?;
            let delta = i32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]) as i64;
            let next = start as i64 + 4;
            (Operand::Branch((next + delta) as u32), 4)
        }
        OperandType::InlineSwitch => {
            let count_bytes = slice_at(code, start, 4, offset)?;
            let count = u32::from_le_bytes([count_bytes[0], count_bytes[1], count_bytes[2], count_bytes[3]]) as usize;
            let total = 4 + count * 4;
            let table = slice_at(code, start, total, offset)?;
            let base = start as i64 + total as i64;
            let mut targets = Vec::with_capacity(count);
            for index in 0..count {
                let at = 4 + index * 4;
                let delta = i32::from_le_bytes([table[at], table[at + 1], table[at + 2], table[at + 3]]) as i64;
                targets.push((base + delta) as u32);
            }
            (Operand::Switch(targets), total)
        }
    })
}
