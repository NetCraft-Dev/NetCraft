//! 托管侧交来的方法体描述
//! 与 IL 字节的区别是分支目标用指令索引 需要外部解析的引用只给名字
//! 这样托管侧不用懂 IL 编码 token 解析与编码都在原生侧做

//类型标签直接沿用 ECMA 335 的元素类型字节 少一层映射
const ELEMENT_VOID: u8 = 0x01;
const ELEMENT_BOOLEAN: u8 = 0x02;
const ELEMENT_CHAR: u8 = 0x03;
const ELEMENT_I1: u8 = 0x04;
const ELEMENT_U1: u8 = 0x05;
const ELEMENT_I2: u8 = 0x06;
const ELEMENT_U2: u8 = 0x07;
const ELEMENT_I4: u8 = 0x08;
const ELEMENT_U4: u8 = 0x09;
const ELEMENT_I8: u8 = 0x0a;
const ELEMENT_U8: u8 = 0x0b;
const ELEMENT_R4: u8 = 0x0c;
const ELEMENT_R8: u8 = 0x0d;
const ELEMENT_STRING: u8 = 0x0e;
const ELEMENT_PTR: u8 = 0x0f;
const ELEMENT_BYREF: u8 = 0x10;
const ELEMENT_VALUETYPE: u8 = 0x11;
const ELEMENT_CLASS: u8 = 0x12;
const ELEMENT_GENERICINST: u8 = 0x15;
const ELEMENT_I: u8 = 0x18;
const ELEMENT_U: u8 = 0x19;
const ELEMENT_OBJECT: u8 = 0x1c;
const ELEMENT_SZARRAY: u8 = 0x1d;

//操作数种类
const KIND_NONE: u8 = 0;
const KIND_I32: u8 = 1;
const KIND_I64: u8 = 2;
const KIND_F32: u8 = 3;
const KIND_F64: u8 = 4;
const KIND_VAR: u8 = 5;
const KIND_BRANCH: u8 = 6;
const KIND_SWITCH: u8 = 7;
const KIND_TOKEN: u8 = 8;
const KIND_REF: u8 = 9;
const KIND_FIELD: u8 = 10;
const KIND_TYPE: u8 = 11;
const KIND_STRING: u8 = 12;

//签名调用约定里的这一位表示实例方法
const SIG_HASTHIS: u8 = 0x20;

//字段签名的首字节 ECMA 335 II.23.2.4 后面直接是字段类型
const FIELD_SIG_TAG: u8 = 0x06;

//Cursor 顺序读字节的小游标 越界就报错
struct Cursor<'a> {
    bytes: &'a [u8],
    position: usize,
}

impl<'a> Cursor<'a> {
    fn new(bytes: &'a [u8]) -> Self {
        Self { bytes, position: 0 }
    }

    fn take(&mut self, count: usize) -> Result<&'a [u8], String> {
        let slice = self
            .bytes
            .get(self.position..self.position + count)
            .ok_or_else(|| format!("描述字节不够 位置 {}", self.position))?;
        self.position += count;
        Ok(slice)
    }

    fn u8(&mut self) -> Result<u8, String> {
        Ok(self.take(1)?[0])
    }

    fn u16(&mut self) -> Result<u16, String> {
        let bytes = self.take(2)?;
        Ok(u16::from_le_bytes([bytes[0], bytes[1]]))
    }

    fn u32(&mut self) -> Result<u32, String> {
        let bytes = self.take(4)?;
        Ok(u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]))
    }

    fn i32(&mut self) -> Result<i32, String> {
        Ok(self.u32()? as i32)
    }

    fn i64(&mut self) -> Result<i64, String> {
        let bytes = self.take(8)?;
        let mut raw = [0u8; 8];
        raw.copy_from_slice(bytes);
        Ok(i64::from_le_bytes(raw))
    }

    fn f32(&mut self) -> Result<f32, String> {
        Ok(f32::from_bits(self.u32()?))
    }

    fn f64(&mut self) -> Result<f64, String> {
        Ok(f64::from_bits(self.i64()? as u64))
    }

    fn text(&mut self) -> Result<String, String> {
        let length = self.u16()? as usize;
        let bytes = self.take(length)?;
        Ok(String::from_utf8_lossy(bytes).into_owned())
    }
}

//TypeDesc 签名里出现的一个类型
#[derive(Clone, Debug)]
pub enum TypeDesc {
    //基本类型 字节本身就是元素类型
    Primitive(u8),
    Class {
        assembly: String,
        name: String,
    },
    ValueType {
        assembly: String,
        name: String,
    },
    SZArray(Box<TypeDesc>),
    ByRef(Box<TypeDesc>),
    Pointer(Box<TypeDesc>),
    //泛型实例化 形状与 ECMA 335 的 GENERICINST 一致
    //kind 是 ELEMENT_CLASS 或 ELEMENT_VALUETYPE 名字是泛型定义 后面跟各个实参
    GenericInst {
        kind: u8,
        assembly: String,
        name: String,
        arguments: Vec<TypeDesc>,
    },
}

//Reference 一条需要解析成运行时 token 的成员引用
#[derive(Clone, Debug)]
pub struct Reference {
    pub assembly: String,
    pub type_name: String,
    pub member: String,
    //实例方法的签名要带 HASTHIS
    pub has_this: bool,
    pub parameters: Vec<TypeDesc>,
    pub return_type: TypeDesc,
}

//FieldReference 一条需要解析成字段 token 的引用
//字段没有参数表 也不需要 HASTHIS 一个字段类型就够了
#[derive(Clone, Debug)]
pub struct FieldReference {
    pub assembly: String,
    pub type_name: String,
    pub name: String,
    pub field_type: TypeDesc,
}

//NamedType 一条需要解析成类型 token 的引用
//ldtoken 这类指令的操作数是一个类型而不是成员 所以单独一张表
#[derive(Clone, Debug)]
pub struct NamedType {
    pub assembly: String,
    pub name: String,
}

//WireOperand 一条指令的操作数
#[derive(Clone, Debug)]
pub enum WireOperand {
    None,
    I32(i32),
    I64(i64),
    F32(f32),
    F64(f64),
    Var(u16),
    //分支目标用指令索引表示
    Branch(usize),
    Switch(Vec<usize>),
    //直接给的 token 同模块已有成员走这条
    Token(u32),
    //需要解析的引用 值是引用表索引
    Ref(usize),
    //需要解析的字段引用 值是字段表索引
    FieldRef(usize),
    //需要解析的类型引用 值是类型表索引
    TypeRef(usize),
    //字符串字面量 值是字符串表索引 内容以 UTF-16 存在表里
    StringRef(usize),
}

#[derive(Clone, Debug)]
pub struct WireInstruction {
    pub code: u16,
    pub operand: WireOperand,
}

#[derive(Clone, Debug)]
pub struct WireBody {
    //bit0 为 1 表示沿用原方法体的局部变量签名 为 0 表示新方法体没有局部变量
    //这一版还不支持给新方法体注入局部变量签名
    pub flags: u8,
    pub max_stack: u16,
    pub instructions: Vec<WireInstruction>,
    pub references: Vec<Reference>,
    pub fields: Vec<FieldReference>,
    pub types: Vec<NamedType>,
    //每项是一段 UTF-16 字节 与 DefineUserString 要的形状一致
    pub strings: Vec<Vec<u8>>,
}

fn parse_type(cursor: &mut Cursor) -> Result<TypeDesc, String> {
    let tag = cursor.u8()?;
    Ok(match tag {
        ELEMENT_VOID | ELEMENT_BOOLEAN | ELEMENT_CHAR | ELEMENT_I1 | ELEMENT_U1 | ELEMENT_I2
        | ELEMENT_U2 | ELEMENT_I4 | ELEMENT_U4 | ELEMENT_I8 | ELEMENT_U8 | ELEMENT_R4
        | ELEMENT_R8 | ELEMENT_STRING | ELEMENT_I | ELEMENT_U | ELEMENT_OBJECT => {
            TypeDesc::Primitive(tag)
        }
        ELEMENT_CLASS => TypeDesc::Class {
            assembly: cursor.text()?,
            name: cursor.text()?,
        },
        ELEMENT_VALUETYPE => TypeDesc::ValueType {
            assembly: cursor.text()?,
            name: cursor.text()?,
        },
        ELEMENT_SZARRAY => TypeDesc::SZArray(Box::new(parse_type(cursor)?)),
        ELEMENT_BYREF => TypeDesc::ByRef(Box::new(parse_type(cursor)?)),
        ELEMENT_PTR => TypeDesc::Pointer(Box::new(parse_type(cursor)?)),
        ELEMENT_GENERICINST => {
            let kind = cursor.u8()?;
            let assembly = cursor.text()?;
            let name = cursor.text()?;
            let count = cursor.u8()? as usize;
            let mut arguments = Vec::with_capacity(count);
            for _ in 0..count {
                arguments.push(parse_type(cursor)?);
            }
            TypeDesc::GenericInst {
                kind,
                assembly,
                name,
                arguments,
            }
        }
        _ => return Err(format!("类型标签不认 0x{tag:02X}")),
    })
}

//parse 把托管侧交来的描述拆开
pub fn parse(bytes: &[u8]) -> Result<WireBody, String> {
    let mut cursor = Cursor::new(bytes);

    let flags = cursor.u8()?;
    let max_stack = cursor.u16()?;
    let instruction_count = cursor.u32()? as usize;
    let mut instructions = Vec::with_capacity(instruction_count);
    for _ in 0..instruction_count {
        let code = cursor.u16()?;
        let kind = cursor.u8()?;
        let operand = match kind {
            KIND_NONE => WireOperand::None,
            KIND_I32 => WireOperand::I32(cursor.i32()?),
            KIND_I64 => WireOperand::I64(cursor.i64()?),
            KIND_F32 => WireOperand::F32(cursor.f32()?),
            KIND_F64 => WireOperand::F64(cursor.f64()?),
            KIND_VAR => WireOperand::Var(cursor.u16()?),
            KIND_BRANCH => WireOperand::Branch(cursor.u32()? as usize),
            KIND_SWITCH => {
                let count = cursor.u32()? as usize;
                let mut targets = Vec::with_capacity(count);
                for _ in 0..count {
                    targets.push(cursor.u32()? as usize);
                }
                WireOperand::Switch(targets)
            }
            KIND_TOKEN => WireOperand::Token(cursor.u32()?),
            KIND_REF => WireOperand::Ref(cursor.u32()? as usize),
            KIND_FIELD => WireOperand::FieldRef(cursor.u32()? as usize),
            KIND_TYPE => WireOperand::TypeRef(cursor.u32()? as usize),
            KIND_STRING => WireOperand::StringRef(cursor.u32()? as usize),
            _ => return Err(format!("操作数种类不认 {kind}")),
        };
        instructions.push(WireInstruction { code, operand });
    }

    let reference_count = cursor.u32()? as usize;
    let mut references = Vec::with_capacity(reference_count);
    for _ in 0..reference_count {
        let assembly = cursor.text()?;
        let type_name = cursor.text()?;
        let member = cursor.text()?;
        let has_this = cursor.u8()? != 0;
        let parameter_count = cursor.u8()? as usize;
        let mut parameters = Vec::with_capacity(parameter_count);
        for _ in 0..parameter_count {
            parameters.push(parse_type(&mut cursor)?);
        }
        let return_type = parse_type(&mut cursor)?;
        references.push(Reference {
            assembly,
            type_name,
            member,
            has_this,
            parameters,
            return_type,
        });
    }

    let field_count = cursor.u32()? as usize;
    let mut fields = Vec::with_capacity(field_count);
    for _ in 0..field_count {
        let assembly = cursor.text()?;
        let type_name = cursor.text()?;
        let name = cursor.text()?;
        let field_type = parse_type(&mut cursor)?;
        fields.push(FieldReference {
            assembly,
            type_name,
            name,
            field_type,
        });
    }

    let type_count = cursor.u32()? as usize;
    let mut types = Vec::with_capacity(type_count);
    for _ in 0..type_count {
        let assembly = cursor.text()?;
        let name = cursor.text()?;
        types.push(NamedType { assembly, name });
    }

    let string_count = cursor.u32()? as usize;
    let mut strings = Vec::with_capacity(string_count);
    for _ in 0..string_count {
        let byte_count = cursor.u16()? as usize;
        let mut bytes = Vec::with_capacity(byte_count);
        for _ in 0..byte_count {
            bytes.push(cursor.u8()?);
        }
        strings.push(bytes);
    }

    Ok(WireBody {
        flags,
        max_stack,
        instructions,
        references,
        fields,
        types,
        strings,
    })
}
//write_compressed_uint ECMA 335 的压缩无符号整数
fn write_compressed_uint(output: &mut Vec<u8>, value: u32) {
    if value < 0x80 {
        output.push(value as u8);
    } else if value < 0x4000 {
        output.push((0x80 | (value >> 8)) as u8);
        output.push((value & 0xFF) as u8);
    } else {
        output.push((0xC0 | (value >> 24)) as u8);
        output.push(((value >> 16) & 0xFF) as u8);
        output.push(((value >> 8) & 0xFF) as u8);
        output.push((value & 0xFF) as u8);
    }
}

//write_type 把一个类型描述写进签名
//自定义类型要转成 TypeDefOrRef 的压缩编码 所以要走 resolve 拿 token
fn write_type<F>(output: &mut Vec<u8>, desc: &TypeDesc, resolve: &F) -> Result<(), String>
where
    F: Fn(&str, &str) -> Result<u32, String>,
{
    match desc {
        TypeDesc::Primitive(tag) => output.push(*tag),
        TypeDesc::Class { assembly, name } => {
            output.push(ELEMENT_CLASS);
            write_compressed_uint(output, resolve(assembly, name)? & 0x00FF_FFFF);
        }
        TypeDesc::ValueType { assembly, name } => {
            output.push(ELEMENT_VALUETYPE);
            write_compressed_uint(output, resolve(assembly, name)? & 0x00FF_FFFF);
        }
        TypeDesc::SZArray(inner) => {
            output.push(ELEMENT_SZARRAY);
            write_type(output, inner, resolve)?;
        }
        TypeDesc::ByRef(inner) => {
            output.push(ELEMENT_BYREF);
            write_type(output, inner, resolve)?;
        }
        TypeDesc::Pointer(inner) => {
            output.push(ELEMENT_PTR);
            write_type(output, inner, resolve)?;
        }
        TypeDesc::GenericInst {
            kind,
            assembly,
            name,
            arguments,
        } => {
            //ECMA 335 II.23.2.12: 标签 随后是 class/valuetype 泛型定义的 TypeDefOrRef 实参个数与实参
            output.push(ELEMENT_GENERICINST);
            output.push(*kind);
            write_compressed_uint(output, resolve(assembly, name)? & 0x00FF_FFFF);
            write_compressed_uint(output, arguments.len() as u32);
            for argument in arguments {
                write_type(output, argument, resolve)?;
            }
        }
    }
    Ok(())
}

//encode_signature 编出 DefineMemberRef 需要的方法签名 blob
pub fn encode_signature<F>(reference: &Reference, resolve: &F) -> Result<Vec<u8>, String>
where
    F: Fn(&str, &str) -> Result<u32, String>,
{
    let mut output = Vec::new();
    output.push(if reference.has_this { SIG_HASTHIS } else { 0x00 });
    write_compressed_uint(&mut output, reference.parameters.len() as u32);
    write_type(&mut output, &reference.return_type, resolve)?;
    for parameter in &reference.parameters {
        write_type(&mut output, parameter, resolve)?;
    }
    Ok(output)
}

//encode_field_signature 编出 DefineFieldRef 需要的字段签名 blob
//首字节是 FIELD 标签 后面跟字段类型 没有参数表也没有调用约定
pub fn encode_field_signature<F>(field: &FieldReference, resolve: &F) -> Result<Vec<u8>, String>
where
    F: Fn(&str, &str) -> Result<u32, String>,
{
    let mut output = Vec::new();
    output.push(FIELD_SIG_TAG);
    write_type(&mut output, &field.field_type, resolve)?;
    Ok(output)
}
