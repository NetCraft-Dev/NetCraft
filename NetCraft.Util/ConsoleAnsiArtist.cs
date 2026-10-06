using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetCraft.Util;

//Console ANSI art
//Provides art printing and style coloring
public class ConsoleAnsiArtist
{
        private static readonly Dictionary<char, string[]> _ansiArtLibrary = new Dictionary<char, string[]>
        {
            {'A', new string[]{" █████╗ ", "██╔══██╗", "███████║", "██╔══██║", "██║  ██║", "╚═╝  ╚═╝"}},
            {'B', new string[]{"██████╗ ", "██╔══██╗", "██████╔╝", "██╔══██╗", "██████╔╝", "╚═════╝ "}},
            {'C', new string[]{" ██████╗", "██╔════╝", "██║     ", "██║     ", "╚██████╗", " ╚═════╝"}},
            {'D', new string[]{"██████╗ ", "██╔══██╗", "██║  ██║", "██║  ██║", "██████╔╝", "╚═════╝ "}},
            {'E', new string[]{"███████╗", "██╔════╝", "█████╗  ", "██╔══╝  ", "███████╗", "╚══════╝"}},
            {'F', new string[]{"███████╗", "██╔════╝", "█████╗  ", "██╔══╝  ", "██║     ", "╚═╝     "}},
            {'G', new string[]{" ██████╗ ", "██╔════╝ ", "██║  ███╗", "██║   ██║", "╚██████╔╝", " ╚═════╝ "}},
            {'H', new string[]{"██╗  ██╗", "██║  ██║", "███████║", "██╔══██║", "██║  ██║", "╚═╝  ╚═╝"}},
            {'I', new string[]{" ████╗ ", " ██╔═╝ ", " ██║   ", " ██║   ", " ██║   ", " ╚═╝   "}},
            {'J', new string[]{"     ██╗", "     ██║", "     ██║", "██   ██║", "╚█████╔╝", " ╚════╝ "}},
            {'K', new string[]{"██╗  ██╗", "██║ ██╔╝", "█████╔╝ ", "██╔═██╗ ", "██║  ██╗", "╚═╝  ╚═╝"}},
            {'L', new string[]{"██╗     ", "██║     ", "██║     ", "██║     ", "███████╗", "╚══════╝"}},
            {'M', new string[]{"███╗   ███╗", "████╗ ████║", "██╔████╔██║", "██║╚██╔╝██║", "██║ ╚═╝ ██║", "╚═╝     ╚═╝"}},
            {'N', new string[]{"███╗   ██╗", "████╗  ██║", "██╔██╗ ██║", "██║╚██╗██║", "██║ ╚████║", "╚═╝  ╚═══╝"}},
            {'O', new string[]{" ██████╗ ", "██╔═══██╗", "██║   ██║", "██║   ██║", "╚██████╔╝", " ╚═════╝ "}},
            {'P', new string[]{"██████╗ ", "██╔══██╗", "██████╔╝", "██╔═══╝ ", "██║     ", "╚═╝     "}},
            {'Q', new string[]{" ██████╗ ", "██╔═══██╗", "██║   ██║", "██║▄▄ ██║", "╚██████╔╝", " ╚══▀▀═╝ "}},
            {'R', new string[]{"██████╗ ", "██╔══██╗", "██████╔╝", "██╔══██╗", "██║  ██║", "╚═╝  ╚═╝"}},
            {'S', new string[]{" ███████╗", "██╔═════╝", "███████╗ ", "╚════██║ ", "███████║ ", "╚══════╝ "}},
            {'T', new string[]{"████████╗", "╚══██╔══╝", "   ██║   ", "   ██║   ", "   ██║   ", "   ╚═╝   "}},
            {'U', new string[]{"██╗   ██╗", "██║   ██║", "██║   ██║", "██║   ██║", "╚██████╔╝", " ╚═════╝ "}},
            {'V', new string[]{"██╗   ██╗", "██║   ██║", "██║   ██║", "╚██╗ ██╔╝", " ╚████╔╝ ", "  ╚═══╝  "}},
            {'W', new string[]{"██╗    ██╗", "██║    ██║", "██║ █╗ ██║", "██║███╗██║", "╚███╔███╔╝", " ╚══╝╚══╝ "}},
            {'X', new string[]{"██╗  ██╗", "╚██╗██╔╝", " ╚███╔╝ ", " ██╔██╗ ", "██╔╝ ██╗", "╚═╝  ╚═╝"}},
            {'Y', new string[]{"██╗   ██╗", "╚██╗ ██╔╝", " ╚████╔╝ ", "  ╚██╔╝  ", "   ██║   ", "   ╚═╝   "}},
            {'Z', new string[]{"███████╗", "╚══███╔╝", "  ███╔╝ ", " ███╔╝  ", "███████╗", "╚══════╝"}},
            {'0', new string[]{" █████╗ ", "██╔══██╗", "██║  ██║", "██║  ██║", "╚██████╔╝", " ╚═════╝ "}},
            {'1', new string[]{" ██╗ ", "███║ ", "╚██║ ", " ██║ ", " ██║ ", " ╚═╝ "}},
            {'2', new string[]{"██████╗ ", "╚════██╗", " █████╔╝", "██╔═══╝ ", "███████╗", "╚══════╝"}},
            {'3', new string[]{"██████╗ ", "╚════██╗", " █████╔╝", " ╚═══██╗", "██████╔╝", "╚═════╝ "}},
            {'4', new string[]{"██╗  ██╗", "██║  ██║", "███████║", "╚════██║", "     ██║", "     ╚═╝"}},
            {'5', new string[]{"███████╗", "██╔════╝", "███████╗", "╚════██║", "███████║", "╚══════╝"}},
            {'6', new string[]{" ██████╗ ", "██╔════╝ ", "███████╗ ", "██╔══██║ ", "╚█████╔╝ ", " ╚════╝  "}},
            {'7', new string[]{"███████╗", "╚════██║", "   ██╔═╝", "   ██║  ", "   ██║  ", "   ╚═╝  "}},
            {'8', new string[]{" █████╗ ", "██╔══██╗", "╚█████╔╝", "██╔══██╗", "╚█████╔╝", " ╚════╝ "}},
            {'9', new string[]{" █████╗ ", "██╔══██╗", "╚██████║", " ╚═══██║", " █████╔╝", " ╚════╝ "}},
            {',', new string[]{"   ", "   ", "   ", "██╗", "██║", "╚═╝"}},
            {'?', new string[]{" ██████╗ ", "██╔═══██╗", "     ██╔╝", "   ██╔╝  ", "   ╚═╝   ", "   ██╗   "}},
            {'\'', new string[]{"██╗", "██║", "╚═╝", "   ", "   ", "   "}},
            {'"', new string[]{"██╗   ██╗", "██║   ██║", "╚═╝   ╚═╝", "        ", "        ", "        "}},
            {'-', new string[]{"   ", "   ", "█████╗", "   ", "   ", "   "}},
            {'!', new string[]{" ██╗ ", " ██║ ", " ██║ ", " ██║ ", " ╚═╝ ", " ██╗ "}},
            {' ', new string[]{"     ", "     ", "     ", "     ", "     ", "     "}},
            {'+', new string[]{"     ", " ██╗ ", "█████╗", " ██╗ ", "     ", "     "}},
            {'*', new string[]{"     ", " ██╗ ", "█████╗", " ██╗ ", "     ", "     "}},
            {'#', new string[]{" ██╗██╗ ", " ██╗██╗ ", "████████╗", " ██╗██╗ ", " ██╗██╗ ", "        "}},
            {'@', new string[]{" ██████╗ ", "██╔═══██╗", "██║ ██╗██║", "██║███╗██║", "╚██████╔╝", " ╚═════╝ "}},
            {'$', new string[]{" ██████╗ ", "██╔════╝ ", "███████╗ ", "╚════██║ ", "███████║ ", "╚══════╝ "}},
            {'%', new string[]{"██╗  ██╗", "██║  ██║", "  ██╔╝  ", " ██╔╝   ", "██║  ██╗", "╚═╝  ╚═╝"}},
            {'^', new string[]{" ██╗ ██╗ ", " ╚██╗██╔╝ ", "  ╚███╔╝  ", "   ╚═╝   ", "        ", "        "}},
            {'&', new string[]{" █████╗ ", "██╔══██╗", "╚█████╔╝", "██╔══██╗", "██║  ██║", "╚═╝  ╚═╝"}},
            {'(', new string[]{" ██╗ ", "██╔╝ ", "██║  ", "██║  ", "██╔╝ ", " ╚═╝ "}},
            {')', new string[]{" ██╗ ", " ╚██╗", "  ██║", "  ██║", " ╚██╗", "  ╚═╝"}},
            {'=', new string[]{"     ", "█████╗", "     ", "█████╗", "     ", "     "}},
            {'[', new string[]{"█████╗", "██╔══╝", "██║   ", "██║   ", "██╔══╝", "█████╗"}},
            {']', new string[]{"█████╗", "╚══██║", "   ██║", "   ██║", "╚══██║", "█████╗"}},
            {'{', new string[]{" ████╗ ", "██╔══╝ ", "██║    ", "██║    ", "██╔══╝ ", " ████╗ "}},
            {'}', new string[]{" ████╗ ", " ╚══██╗", "    ██║", "    ██║", " ╚══██╗", " ████╗ "}},
            {'|', new string[]{" ██╗ ", " ██║ ", " ██║ ", " ██║ ", " ██║ ", " ╚═╝ "}},
            {';', new string[]{"     ", "     ", " ██╗ ", " ██║ ", " ██║ ", " ╚═╝ "}},
            {':', new string[]{"     ", " ██╗ ", " ╚═╝ ", " ██╗ ", " ╚═╝ ", "     "}},
            {'<', new string[]{"   ██╗", " ██╔╝ ", "██╔╝  ", "██╔╝  ", " ██╔╝ ", "   ╚═╝"}},
            {'>', new string[]{"██╗   ", "╚██╗  ", " ╚██╗ ", " ╚██╗ ", "  ██╔╝", "  ╚═╝ "}},
            {'/', new string[]{"     ██╗", "    ██╔╝", "   ██╔╝ ", "  ██╔╝  ", " ██╔╝   ", "██╔╝    "}},
            {'\\', new string[]{"██╗     ", "╚██╗    ", " ╚██╗   ", "  ╚██╗  ", "   ╚██╗ ", "    ╚██╗"}},
            {'_', new string[]{"     ", "     ", "     ", "     ", "     ", "█████"}},
            {'`', new string[]{" ██╗ ", " ╚██╗", "  ╚═╝", "     ", "     ", "     "}},
            {'~', new string[]{"     ", " ██╗ ", "╚═╝██╗", "  ╚═╝ ", "     ", "     "}},
        };

        public enum GradientDirection { Horizontal, Vertical, Diagonal }

        private static string InterpolateColor(string color1, string color2, float ratio)
        {
            var c1 = color1.Split(',').Select(int.Parse).ToArray();
            var c2 = color2.Split(',').Select(int.Parse).ToArray();
            int r = (int)(c1[0] + (c2[0] - c1[0]) * ratio);
            int g = (int)(c1[1] + (c2[1] - c1[1]) * ratio);
            int b = (int)(c1[2] + (c2[2] - c1[2]) * ratio);
            return $"{Math.Clamp(r, 0, 255)},{Math.Clamp(g, 0, 255)},{Math.Clamp(b, 0, 255)}";
        }

        private static string GetRainbowColor(float ratio)
        {
            float h = ratio * 360f;
            float s = 1.0f;
            float v = 1.0f;
            int hi = (int)Math.Floor(h / 60f) % 6;
            float f = h / 60f - (float)Math.Floor(h / 60f);
            float p = v * (1 - s);
            float q = v * (1 - f * s);
            float t = v * (1 - (1 - f) * s);
            float r, g, b;
            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return $"{(int)(r * 255)},{(int)(g * 255)},{(int)(b * 255)}";
        }

        private static string GetHeatmapColor(float ratio)
        {
            if (ratio < 0.5f)
            {
                float t = ratio / 0.5f;
                int r = (int)(255 * t);
                int g = (int)(255 * (1 - t));
                int b = 0;
                return $"{r},{g},{b}";
            }
            else
            {
                float t = (ratio - 0.5f) / 0.5f;
                int r = 255;
                int g = (int)(255 * (1 - t));
                int b = 0;
                return $"{r},{g},{b}";
            }
        }

        private static bool IsValidRgbColor(string rgbString)
        {
            var parts = rgbString.Split(',');
            if (parts.Length != 3) return false;
            foreach (var part in parts)
                if (!int.TryParse(part.Trim(), out int value) || value < 0 || value > 255) return false;
            return true;
        }

        private static string GetAnsiColorCode(string rgbString)
        {
            var parts = rgbString.Split(',').Select(p => int.Parse(p.Trim())).ToArray();
            return $"\u001b[38;2;{parts[0]};{parts[1]};{parts[2]}m";
        }

        private static string GetAnsiBackgroundColorCode(string rgbString)
        {
            var parts = rgbString.Split(',').Select(p => int.Parse(p.Trim())).ToArray();
            return $"\u001b[48;2;{parts[0]};{parts[1]};{parts[2]}m";
        }

        private static string GetAnsiResetCode() => "\u001b[0m";

        private static char GetCharKey(char c)
        {
            if (c == ',' || c == '?' || c == '\'' || c == '"' || c == '-' || c == '!' || c == ' ' ||
                c == '+' || c == '*' || c == '#' || c == '@' || c == '$' || c == '%' || c == '^' ||
                c == '&' || c == '(' || c == ')' || c == '=' || c == '[' || c == ']' || c == '{' ||
                c == '}' || c == '|' || c == ';' || c == ':' || c == '<' || c == '>' || c == '/' ||
                c == '\\' || c == '_' || c == '`' || c == '~' || c == '.')
                return c;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                return char.ToUpper(c);
            return c;
        }

        private static bool IsChineseCharacter(char c) => (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF);

        public static void PrintAnsiText(string text) => PrintAnsiText(text, "", "");

        public static void PrintAnsiText(string text, string foregroundColor, string? backgroundColor = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            bool hasValidForeground = !string.IsNullOrEmpty(foregroundColor) && IsValidRgbColor(foregroundColor);
            bool hasValidBackground = !string.IsNullOrEmpty(backgroundColor) && IsValidRgbColor(backgroundColor);

            string colorPrefix = "";
            if (hasValidForeground) colorPrefix += GetAnsiColorCode(foregroundColor);
            if (hasValidBackground) colorPrefix += GetAnsiBackgroundColorCode(backgroundColor!);
            string colorSuffix = string.IsNullOrEmpty(colorPrefix) ? "" : GetAnsiResetCode();

            bool canPrintAsAnsi = text.All(c => _ansiArtLibrary.ContainsKey(GetCharKey(c)) || c == ' ' || IsChineseCharacter(c));

            if (!canPrintAsAnsi)
            {
                if (!string.IsNullOrEmpty(colorPrefix)) { Console.Write(colorPrefix); Console.Write(text); Console.WriteLine(colorSuffix); }
                else Console.WriteLine(text);
                return;
            }

            int maxHeight = 0;
            foreach (char c in text)
            {
                char key = GetCharKey(c);
                if (_ansiArtLibrary.ContainsKey(key))
                    maxHeight = Math.Max(maxHeight, _ansiArtLibrary[key].Length);
            }

            if (maxHeight == 0)
            {
                if (!string.IsNullOrEmpty(colorPrefix)) { Console.Write(colorPrefix); Console.Write(text); Console.WriteLine(colorSuffix); }
                else Console.WriteLine(text);
                return;
            }

            for (int line = 0; line < maxHeight; line++)
            {
                StringBuilder lineBuilder = new StringBuilder();
                if (!string.IsNullOrEmpty(colorPrefix)) lineBuilder.Append(colorPrefix);

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    char key = GetCharKey(c);

                    if (c == ' ')
                    {
                        lineBuilder.Append("     ");
                    }
                    else if (IsChineseCharacter(c))
                    {
                        if (line == 0) lineBuilder.Append(c);
                        else lineBuilder.Append("  ");
                    }
                    else if (_ansiArtLibrary.ContainsKey(key))
                    {
                        var artLines = _ansiArtLibrary[key];
                        if (line < artLines.Length)
                        {
                            lineBuilder.Append(artLines[line]);
                        }
                        else if (artLines.Length > 0)
                        {
                            lineBuilder.Append(new string(' ', artLines[0].Length));
                        }
                        else
                        {
                            lineBuilder.Append("   ");
                        }

                        if (i < text.Length - 1 && text[i + 1] != ' ')
                        {
                            lineBuilder.Append(' ');
                        }
                    }
                }

                if (!string.IsNullOrEmpty(colorSuffix)) lineBuilder.Append(colorSuffix);
                Console.WriteLine(lineBuilder.ToString());
            }
        }

        public static void PrintGradientText(string text, string startColor, string endColor, GradientDirection direction = GradientDirection.Horizontal)
        {
            if (string.IsNullOrEmpty(text) || !IsValidRgbColor(startColor) || !IsValidRgbColor(endColor))
            {
                PrintAnsiText(text);
                return;
            }

            bool canPrintAsAnsi = text.All(c => _ansiArtLibrary.ContainsKey(GetCharKey(c)) || c == ' ' || IsChineseCharacter(c));
            if (!canPrintAsAnsi)
            {
                Console.WriteLine(text);
                return;
            }

            int maxHeight = 0;
            int totalWidth = 0;
            foreach (char c in text)
            {
                char key = GetCharKey(c);
                if (_ansiArtLibrary.ContainsKey(key))
                {
                    int h = _ansiArtLibrary[key].Length;
                    maxHeight = Math.Max(maxHeight, h);
                    if (_ansiArtLibrary[key].Length > 0)
                        totalWidth += _ansiArtLibrary[key][0].Length + 1;
                }
                else if (c == ' ')
                    totalWidth += 5;
                else if (IsChineseCharacter(c))
                    totalWidth += 2;
            }

            if (maxHeight == 0 || totalWidth == 0)
            {
                PrintAnsiText(text);
                return;
            }

            for (int line = 0; line < maxHeight; line++)
            {
                int currentPos = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    char key = GetCharKey(c);

                    if (c == ' ')
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            float ratio = (float)currentPos / (totalWidth - 1);
                            string color = InterpolateColor(startColor, endColor, Math.Clamp(ratio, 0, 1));
                            Console.Write(GetAnsiColorCode(color) + " ");
                            currentPos++;
                        }
                    }
                    else if (_ansiArtLibrary.ContainsKey(key))
                    {
                        var artLines = _ansiArtLibrary[key];
                        if (line < artLines.Length)
                        {
                            string artLine = artLines[line];
                            for (int j = 0; j < artLine.Length; j++)
                            {
                                float ratio = 0;
                                if (direction == GradientDirection.Horizontal)
                                    ratio = (float)currentPos / (totalWidth - 1);
                                else if (direction == GradientDirection.Vertical)
                                    ratio = (float)line / (maxHeight - 1);
                                else
                                    ratio = ((float)(line + currentPos) / (maxHeight + totalWidth));

                                string color = InterpolateColor(startColor, endColor, Math.Clamp(ratio, 0, 1));
                                Console.Write(GetAnsiColorCode(color) + artLine[j]);
                                currentPos++;
                            }
                            if (i < text.Length - 1 && text[i + 1] != ' ')
                            {
                                Console.Write(" ");
                                currentPos++;
                            }
                        }
                    }
                    else if (IsChineseCharacter(c))
                    {
                        if (line == 0)
                        {
                            float ratio = (float)currentPos / (totalWidth - 1);
                            string color = InterpolateColor(startColor, endColor, Math.Clamp(ratio, 0, 1));
                            Console.Write(GetAnsiColorCode(color) + c);
                            currentPos += 2;
                        }
                        else
                        {
                            Console.Write("  ");
                            currentPos += 2;
                        }
                    }
                }
                Console.WriteLine(GetAnsiResetCode());
            }
        }

        public static void PrintRainbowText(string text, GradientDirection direction = GradientDirection.Horizontal)
        {
            if (string.IsNullOrEmpty(text))
                return;

            bool canPrintAsAnsi = text.All(c => _ansiArtLibrary.ContainsKey(GetCharKey(c)) || c == ' ' || IsChineseCharacter(c));
            if (!canPrintAsAnsi)
            {
                Console.WriteLine(text);
                return;
            }

            int maxHeight = 0;
            int totalWidth = 0;
            foreach (char c in text)
            {
                char key = GetCharKey(c);
                if (_ansiArtLibrary.ContainsKey(key))
                {
                    int h = _ansiArtLibrary[key].Length;
                    maxHeight = Math.Max(maxHeight, h);
                    if (_ansiArtLibrary[key].Length > 0)
                        totalWidth += _ansiArtLibrary[key][0].Length + 1;
                }
                else if (c == ' ')
                    totalWidth += 5;
                else if (IsChineseCharacter(c))
                    totalWidth += 2;
            }

            if (maxHeight == 0 || totalWidth == 0)
            {
                Console.WriteLine(text);
                return;
            }

            for (int line = 0; line < maxHeight; line++)
            {
                int currentPos = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    char key = GetCharKey(c);

                    if (c == ' ')
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            float ratio = (float)currentPos / (totalWidth - 1);
                            string color = GetRainbowColor(Math.Clamp(ratio, 0, 1));
                            Console.Write(GetAnsiColorCode(color) + " ");
                            currentPos++;
                        }
                    }
                    else if (_ansiArtLibrary.ContainsKey(key))
                    {
                        var artLines = _ansiArtLibrary[key];
                        if (line < artLines.Length)
                        {
                            string artLine = artLines[line];
                            for (int j = 0; j < artLine.Length; j++)
                            {
                                float ratio = 0;
                                if (direction == GradientDirection.Horizontal)
                                    ratio = (float)currentPos / (totalWidth - 1);
                                else if (direction == GradientDirection.Vertical)
                                    ratio = (float)line / (maxHeight - 1);
                                else
                                    ratio = ((float)(line + currentPos) / (maxHeight + totalWidth));

                                string color = GetRainbowColor(Math.Clamp(ratio, 0, 1));
                                Console.Write(GetAnsiColorCode(color) + artLine[j]);
                                currentPos++;
                            }
                            if (i < text.Length - 1 && text[i + 1] != ' ')
                            {
                                Console.Write(" ");
                                currentPos++;
                            }
                        }
                    }
                    else if (IsChineseCharacter(c))
                    {
                        if (line == 0)
                        {
                            float ratio = (float)currentPos / (totalWidth - 1);
                            string color = GetRainbowColor(Math.Clamp(ratio, 0, 1));
                            Console.Write(GetAnsiColorCode(color) + c);
                            currentPos += 2;
                        }
                        else
                        {
                            Console.Write("  ");
                            currentPos += 2;
                        }
                    }
                }
                Console.WriteLine(GetAnsiResetCode());
            }
        }

        public static void PrintHeatmapText(string text, GradientDirection direction = GradientDirection.Horizontal)
        {
            if (string.IsNullOrEmpty(text))
                return;

            bool canPrintAsAnsi = text.All(c => _ansiArtLibrary.ContainsKey(GetCharKey(c)) || c == ' ' || IsChineseCharacter(c));
            if (!canPrintAsAnsi)
            {
                Console.WriteLine(text);
                return;
            }

            int maxHeight = 0;
            int totalWidth = 0;
            foreach (char c in text)
            {
                char key = GetCharKey(c);
                if (_ansiArtLibrary.ContainsKey(key))
                {
                    int h = _ansiArtLibrary[key].Length;
                    maxHeight = Math.Max(maxHeight, h);
                    if (_ansiArtLibrary[key].Length > 0)
                        totalWidth += _ansiArtLibrary[key][0].Length + 1;
                }
                else if (c == ' ')
                    totalWidth += 5;
                else if (IsChineseCharacter(c))
                    totalWidth += 2;
            }

            if (maxHeight == 0 || totalWidth == 0)
            {
                Console.WriteLine(text);
                return;
            }

            for (int line = 0; line < maxHeight; line++)
            {
                int currentPos = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    char key = GetCharKey(c);

                    if (c == ' ')
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            float ratio = (float)currentPos / (totalWidth - 1);
                            string color = GetHeatmapColor(Math.Clamp(ratio, 0, 1));
                            Console.Write(GetAnsiColorCode(color) + " ");
                            currentPos++;
                        }
                    }
                    else if (_ansiArtLibrary.ContainsKey(key))
                    {
                        var artLines = _ansiArtLibrary[key];
                        if (line < artLines.Length)
                        {
                            string artLine = artLines[line];
                            for (int j = 0; j < artLine.Length; j++)
                            {
                                float ratio = 0;
                                if (direction == GradientDirection.Horizontal)
                                    ratio = (float)currentPos / (totalWidth - 1);
                                else if (direction == GradientDirection.Vertical)
                                    ratio = (float)line / (maxHeight - 1);
                                else
                                    ratio = ((float)(line + currentPos) / (maxHeight + totalWidth));

                                string color = GetHeatmapColor(Math.Clamp(ratio, 0, 1));
                                Console.Write(GetAnsiColorCode(color) + artLine[j]);
                                currentPos++;
                            }
                            if (i < text.Length - 1 && text[i + 1] != ' ')
                            {
                                Console.Write(" ");
                                currentPos++;
                            }
                        }
                    }
                    else if (IsChineseCharacter(c))
                    {
                        if (line == 0)
                        {
                            float ratio = (float)currentPos / (totalWidth - 1);
                            string color = GetHeatmapColor(Math.Clamp(ratio, 0, 1));
                            Console.Write(GetAnsiColorCode(color) + c);
                            currentPos += 2;
                        }
                        else
                        {
                            Console.Write("  ");
                            currentPos += 2;
                        }
                    }
                }
                Console.WriteLine(GetAnsiResetCode());
            }
        }

        public static void AddCustomCharacter(char character, string[] artLines) => _ansiArtLibrary[character] = artLines;
    }