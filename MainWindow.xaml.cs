using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BoolSolver
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        
        // Функция которая вызывается при нажатии кнопки решить
        private void Solve(object o, RoutedEventArgs args)
        {
            if (expressionBox.Text.Length == 0)
                return;
            
            TruthTableBox.Clear();
            PCNFBox.Clear();
            PDNFBox.Clear();
            try
            {
                // Составляем ОПЗ
                Stack<Token> rpn = RPN.ToRPN(expressionBox.Text);
                // Составляем сортированный словарь переменных
                SortedDictionary<char, bool> variablesTable = RPN.getVariableDict(rpn);
                // Переменная будет хранить вектор функции
                List<bool> answerVector = new List<bool>();

                string PDNF = "";
                string PCNF = "";
                
                // Делаем шапку для нашей таблицы
                foreach (var (k, _) in variablesTable)
                    TruthTableBox.Text += $"{k} ";
                TruthTableBox.Text += "Ответ\n";
                
                // Количество строчек = 2 ^ количество переменных
                for (int i = 0; i < 2 << variablesTable.Count - 1; i++)
                {
                    // Записываем значения переменных в строку
                    foreach (var (k, v) in variablesTable)
                        TruthTableBox.Text += Convert.ToInt32(v) + " ";

                    try
                    {
                        // Пытаемся решить ОПЗ с текущими значениями переменныъ
                        bool answer = RPN.Solve(variablesTable, rpn);
                        
                        // Если ответ единица, то расширяем СДНФ
                        if (answer)
                        {
                            if (PDNF.Length != 0)
                                PDNF += " | ";
                            PDNF += "(";
                            for (int j = 0; j < variablesTable.Count; j++)
                            {
                                var (k, v) = variablesTable.ElementAt(j);
                                
                                PDNF += !v ? $"!{k}" : $"{k}";
                                
                                if (j != variablesTable.Count - 1)
                                    PDNF += " & ";
                            }
                            PDNF += ")";
                        }
                        // Eсли ответ ноль, то расширяем СКНФ
                        else
                        {
                            if (PCNF.Length != 0)
                                PCNF += " & ";
                            PCNF += "(";
                            for (int j = 0; j < variablesTable.Count; j++)
                            {
                                var (k, v) = variablesTable.ElementAt(j);

                                PCNF += v ? $"!{k}" : $"{k}";

                                if (j != variablesTable.Count - 1)
                                    PCNF += " | ";
                            }
                            PCNF += ")";
                        }
                        
                        // Добавляем значение выражения в вектор функции
                        answerVector.Add(answer);
                        TruthTableBox.Text += "  " + Convert.ToInt32(answer) + '\n';
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        TruthTableBox.Clear();
                        return;
                    }
                    
                    // Сдвигаем текущий набор переменных к сделдующему. Пример: 00 -> 01 -> 10 -> 11
                    RPN.ShiftVariables(ref variablesTable);
                }
                
                // Из листа формируем строку
                string answerVectorString = "";
                foreach (var i in answerVector)
                {
                    answerVectorString += Convert.ToInt32(i);
                }

                // Записываем ответы в TextBox
                TruthTableBox.Text += $"Вектор функции:\n{answerVectorString}\n";
                PCNFBox.Text += $"{PCNF}";
                PDNFBox.Text += $"{PDNF}";
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    enum TokenType
    {
        OPERATION,
        VARIABLE
    };

    enum Operation
    {
        AND,
        OR,
        NOT,
        XOR,
        EQUAL,
        IMPL,
        LBRC,
        RBRC
    }
    
    // Абстрактный класс для любого токена
    abstract class Token
    {
        public TokenType Type { get; }

        protected Token(TokenType type)
        {
            Type = type;
        }

        public static Token CharToToken(char c)
        {
            if (c is >= 'a' and <= 'z' || c is >= 'A' and <= 'Z')
            {
                return new VariableToken(c);
            }

            switch (c)
            {
                case '!':
                    return new OperationToken(Operation.NOT);
                case '&':
                    return new OperationToken(Operation.AND);
                case '|':
                    return new OperationToken(Operation.OR);
                case '^':
                    return new OperationToken(Operation.XOR);
                case '=':
                    return new OperationToken(Operation.EQUAL);
                case '@':
                    return new OperationToken(Operation.IMPL);
                case '(':
                    return new OperationToken(Operation.LBRC);
                case ')':
                    return new OperationToken(Operation.RBRC);
                default:
                    throw new Exception($"Invalid letter {c} in expression!");
            }
        }
    }
    
    // Класс токена операции
    class OperationToken : Token
    {
        public readonly Operation Operation;

        public OperationToken(Operation operation) : base(TokenType.OPERATION)
        {
            Operation = operation;
        }

        public char ToChar()
        {
            return Operation switch
            {
                Operation.AND => '&',
                Operation.NOT => '!',
                Operation.OR => '|',
                Operation.XOR => '^',
                Operation.IMPL => '@',
                Operation.EQUAL => '=',
                _ => ' '
            };
        }
    }
    
    // Класс токена переменной
    class VariableToken : Token
    {
        public bool Value { get; }
        public bool Inverted { get; set; }
        public char Letter { get; private set; }

        public VariableToken(char let, bool value = false) : base(TokenType.VARIABLE)
        {
            Letter = let;
            Value = value;
            Inverted = false;
        }
    }
    
    // Статический класс, отвечающий за преобразования и решение выражений
    static class RPN
    {
        static int getPrior(OperationToken token)
        {
            switch (token.Operation)
            {
                case Operation.NOT:
                    return 5;
                case Operation.AND:
                    return 4;
                case Operation.OR:
                case Operation.XOR:
                    return 3;
                case Operation.IMPL:
                    return 2;
                case Operation.EQUAL:
                    return 1;
                default:
                    return 0;
            }
        }
        
        // Функция которая переводит инфксную запись выражения (A & B)
        // в обратную польскую запись (AB&)
        public static Stack<Token> ToRPN(string expression)
        {
            Stack<OperationToken> OperationStack = new Stack<OperationToken>();
            Stack<Token> OutStack = new Stack<Token>();

            bool MayNOT = true;
            bool MayOPERATOR = true;
            bool NOT = false;

            foreach (char c in expression)
            {
                if (c == ' ') continue;

                Token token = Token.CharToToken(c);
                if (token.Type == TokenType.VARIABLE)
                {
                    VariableToken variableToken = (VariableToken) token;
                    variableToken.Inverted = NOT;
                    OutStack.Push(token);

                    MayNOT = false;
                    NOT = false;
                    MayOPERATOR = true;
                }

                else
                {
                    OperationToken operation = (OperationToken) token;

                    if (operation.Operation == Operation.NOT)
                    {
                        if (MayNOT)
                            NOT = !NOT;
                        else
                            throw new Exception("Invalid expression!");
                    }
                    else
                    {
                        MayNOT = true;
                        if (operation.Operation == Operation.LBRC)
                        {
                            if (NOT)
                                throw new Exception("Unsupported expression!");

                            OperationStack.Push(operation);
                            MayOPERATOR = false;
                        }
                        else if (operation.Operation == Operation.RBRC)
                        {
                            while (OperationStack.Count != 0)
                            {
                                OperationToken other_operation = OperationStack.Pop();
                                if (other_operation.Operation == Operation.LBRC)
                                    break;
                                OutStack.Push(other_operation);
                            }

                            MayOPERATOR = true;
                        }
                        else if (MayOPERATOR)
                        {
                            while (OperationStack.Count != 0)
                            {
                                if (getPrior(operation) <= getPrior(OperationStack.Peek()))
                                    OutStack.Push(OperationStack.Pop());
                                else break;
                            }

                            OperationStack.Push(operation);
                            MayOPERATOR = false;
                        }
                        else
                            throw new Exception("Invalid expression!");
                    }
                }
            }

            while (OperationStack.Count != 0)
            {
                OperationToken operation = OperationStack.Pop();
                if (operation.Operation == Operation.LBRC)
                    throw new Exception("Invalid expression!");
                OutStack.Push(operation);
            }

            return OutStack;
        }
        
        // Функция которая рещшает выражение по заданному набору значений переменных
        // и обратной польской записи
        public static bool Solve(SortedDictionary<char, bool> variables, Stack<Token> rpn)
        {
            Stack<bool> solvedStack = new Stack<bool>();
            foreach (var token in rpn.Reverse())
            {
                try
                {
                    if (token.Type == TokenType.VARIABLE)
                    {
                        VariableToken variableToken = (VariableToken) token;
                        if (variableToken.Inverted) solvedStack.Push(!variables[variableToken.Letter]);
                        else solvedStack.Push(variables[variableToken.Letter]);
                    }
                    else
                    {
                        OperationToken operationToken = (OperationToken) token;
                        bool X = solvedStack.Pop();
                        bool Y = solvedStack.Pop();

                        switch (operationToken.Operation)
                        {
                            case Operation.AND:
                                solvedStack.Push(X & Y);
                                break;
                            case Operation.OR:
                                solvedStack.Push(X | Y);
                                break;
                            case Operation.XOR:
                                solvedStack.Push(X & !Y | !X & Y);
                                break;
                            case Operation.EQUAL:
                                solvedStack.Push(!X && !Y || X && Y);
                                break;
                            case Operation.IMPL:
                                solvedStack.Push(!X | Y);
                                break;
                            default:
                                throw new Exception($"Invalid operation {operationToken.ToChar()} in RPN!");
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Invalid expression.\nDetail: {e.Message}");
                }
            }

            return solvedStack.Pop();
        }
        
        // Функция, которая составляет отсортированный словарь переменных и их текущих значений
        public static SortedDictionary<char, bool> getVariableDict(Stack<Token> rpn)
        {
            SortedDictionary<char, bool> variablesTable = new SortedDictionary<char, bool>();
            foreach (Token token in rpn)
            {
                if (token.Type == TokenType.VARIABLE)
                {
                    VariableToken variable = (VariableToken) token;
                    if (!variablesTable.ContainsKey(variable.Letter))
                        variablesTable.Add(variable.Letter, false);
                }
            }

            return variablesTable;
        }
        
        // Функция, которая сдвигает текущие значения переменных в словаре переменных.
        // Пример: 000 -> 001 -> 010 -> 011
        public static void ShiftVariables(ref SortedDictionary<char, bool> variablesTable)
        {
            for (int j = variablesTable.Count - 1; j >= 0; j--)
            {
                var (key, value) = variablesTable.ElementAt(j);
                if (!value)
                {
                    variablesTable[key] = true;
                    break;
                }

                variablesTable[key] = false;
            }
        }
    }
}