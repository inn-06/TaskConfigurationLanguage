using System.Text;
using System.Text.RegularExpressions;

namespace MyConfigConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            string? inputPath = string.Empty;
            string? outputPath = string.Empty;
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--input" && i + 1 < args.Length)
                    {
                        inputPath = args[++i];
                    }
                    else if (args[i] == "--output" && i + 1 < args.Length)
                    {
                        outputPath = args[++i];
                    }
                }
            }
            else
            {
                Console.WriteLine("Введите путь файла, который следует преобразовать");
                inputPath = Console.ReadLine();
                Console.WriteLine("Введите путь к файлу в который будет поступать выходная информация");
                outputPath = Console.ReadLine();
            }
                

            Validation(inputPath, outputPath);
            try
            {
                var source = File.ReadAllText(inputPath);
                var result = Parser.Parse(source);
                
                //Записывает результат в файл
                File.WriteAllText(outputPath, result);
                if (Parser.CollectionRawStrings.Length > 0)
                {
                    OutputRawStringsConsol(Parser.CollectionRawStrings);
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.ReadLine();
            }
        }
        /// <summary>
        /// Валидация вводимых и выходных данных
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="outputPath"></param>
        private static void Validation(string inputPath, string outputPath)
        {
            if (string.IsNullOrEmpty(inputPath) || string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine("Параметр(ы) не был(и) введены правильно");
                Console.ReadLine();
            }

            else if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Файл по пути \"{inputPath}\" отсутствует");
                Console.ReadLine();
            }
            else if (!File.Exists(outputPath))
            {
                Console.WriteLine($"Файл по пути \"{outputPath}\" отсутствует");
                Console.ReadLine();
            }
            else if (!outputPath.EndsWith("toml"))
            {
                Console.WriteLine($"Неверное расширение выходного файла {outputPath}. Отсутствует расширение формата \"toml\".");
                Console.ReadLine();
            }
        }
        private static void OutputRawStringsConsol(StringBuilder CollectionRawStrings)
        {
            Console.WriteLine("Необработанные строки: ");
            Console.WriteLine(CollectionRawStrings.ToString());
        }
    }

    public static class Parser
    {
        public static readonly StringBuilder CollectionRawStrings = new();
        private static readonly Dictionary<string, object> constants = [];
        //StringBuilder. оптимизированный класс который упращает взаимодействие со строками
        private static readonly StringBuilder toml = new();

        public static string Parse(string source)
        {
            // Удаляем многострочные комментарии
            var cleanSource = Regex.Replace(source, @"{{!\s*--[\s\S]*?--}}", "", RegexOptions.Multiline);

            // Разбиваем на строки
            var lines = cleanSource.Split('\n');

            // Проходим по строкам, собираем константы
            var statements = new List<string>();
            foreach (var line in lines)
            {
                //Метод Trim удаляет лишние пробелы
                var trimmed = line.Trim();
                //Если строка пустая или null то пропускаем, а так же если есть символ "#" в начале, такие строки мы тоже не обрабатываем
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) 
                    continue;
                //Добавляем стоку в список
                statements.Add(trimmed);
            }

            // парсим константы
            var remainingLines = new List<string>();
            foreach (var statement in statements)
            {
                //Регулярное вырожение. 
                if (Regex.IsMatch(statement, @"^[_a-z]+ := .+$"))
                {
                    //Разделяем строки на массив из двух элементов по условию " := "
                    var parts = statement.Split(" := ", 2);
                    var name = parts[0].Trim();
                    var value = ParseValue(parts[1].Trim());
                    //Добавляем в словарь константы, где ключ наша строка, а значение наше "value"
                    constants[name] = value;
                }
                else
                {
                    remainingLines.Add(statement);
                }
            }

            // Парсим остальные выражения (таблицы и присваивания)
            ParsHandling(remainingLines);
            return toml.ToString();
        }

        private static object ParseValue(string value)
        {
            // Проверка двоичного числа
            //Преобрауем нашу строку в число
            var binaryMatch = Regex.Match(value, @"^0[bB][01]+$");
            if (binaryMatch.Success)
            {
                var binaryStr = value.Substring(2);
                return Convert.ToInt32(binaryStr, 2);
            }

            // Проверка строки
            if (value.StartsWith("'") && value.EndsWith("'"))
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }
        /// <summary>
        /// Основной метод парсинга строк
        /// </summary>
        /// <param name="lines"></param>
        private static void ParsHandling(List<string> lines)
        {
            //Ключ - название таблицы, например "server". Изначально объявляем пустую строку
            string key = string.Empty;
            //Словарь с ключoм и значением. 
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < lines.Count; i++)
            {
                //Получаем строку
                string line = lines[i];
                //Пропускамем пустые
                if (string.IsNullOrEmpty(line))
                    continue;

                var assignment = Regex.Match(line, @"^([_a-z]+)\s*=\s*(.+)$");
                if (assignment.Success)
                {
                    if (line.Contains("table"))
                        key = assignment.Groups[1].Value;
                    else
                    {
                        string pars = ReplaceConstants(line);
                        toml.AppendLine(pars);
                    }
                    continue;
                }
                else if (line.Contains('-'))
                {
                    var pair = line.Split("-", 2);
                    var k = pair[0].Trim();
                    var v = ReplaceConstants(pair[1].Trim());
                    dict.Add(k, v);
                }
                else if (line.Contains("])"))
                {
                    AppendToToml(key, dict);
                    dict.Clear();
                }
                else
                {
                    CollectionRawStrings.AppendLine(line);
                }
            }
        }
        /// <summary>
        /// Данный метод преобразует строку в конечный результат, подставляя вместо имени переменной, его значение
        /// </summary>
        /// <param name="line">Строка для преобразования</param>
        /// <returns></returns>
        private static string ReplaceConstants(string line)
        {
            var result = line;
            foreach (var kvp in constants)
            {
                var placeholder = "$" + kvp.Key + "$";
                var replacement = FormatValue(kvp.Value);
                //Метод Replace заменяет старое значение на новое
                result = result.Replace(placeholder, replacement);
            }
            if (result.StartsWith("'") && result.EndsWith(","))
                return result[1..^2];
            if (result.EndsWith(","))
                return result[..^1]; 
            return result;
        }
        /// <summary>
        /// Данный метод представляет из себя получение значения по вводимому объекту. 
        /// Если это число, то вернёт стоку
        /// Иначе выдаст ошибку, что были введены неверные данные
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private static string FormatValue(object value)
        {
            return value switch
            {
                string s => $"'{s}'",
                int i => i.ToString(),
                _ => throw new NotSupportedException()
            };
        }
        /// <summary>
        /// Добавляет строку в конечный результат
        /// </summary>
        /// <param name="key">Ключ - название таблицы </param>
        /// <param name="value">Объект (string, Dictionary(словарь) или int)</param>
        /// <param name="prefix">Значение которое будет идти перед ключом</param>
        /// <exception cref="NotSupportedException"></exception>
        private static void AppendToToml(string key, object value, string prefix = "")
        {
            //Елси prefix пуст, то возвращает значение key иначе prefix.key (вместо prefix и key их значения).
            var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            switch (value)
            {
                case Dictionary<string, string> dict:
                    toml.AppendLine($"[{fullKey}]");
                    foreach (var kvp in dict)
                    {
                        AppendToToml(kvp.Key, kvp.Value, "");
                    }
                    break;
                case string s:
                    toml.AppendLine($"{fullKey} = '{s}'");
                    break;
                case int i:
                    toml.AppendLine($"{fullKey} = {i}");
                    break;
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
        
    
        
