using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Hypertherm.CcCli
{
    public class ArgumentHandler
    {
        private static Args _argData;
        public Args ArgData => _argData;
        public bool HasValidArguments() => !ArgData.IsError;

        private List<string> _args;
        public ArgumentHandler(string[] args)
        {
            _argData = "XLSX";
            _args = CleanArgs(args);
            ValidateArguments();
        }

        private List<string> CleanArgs(string[] args)
        {
            List<string> cleanArgs = new List<string>();
            bool isFilename = false;

            for (int i = 0; i < args.Length; i++)
            {
                // If the previous option/switch expects a filename to follow
                // and that filename was valid we don't apply ToLower to it when
                // adding it to the cleaned up args List.
                cleanArgs.Add(isFilename ? args[i] : args[i].ToLower());
                isFilename = false;

                if (args[i] == "-o"
                || args[i] == "--outfile"
                || args[i] == "-x"
                || args[i] == "--xmlfile")
                {
                    if (args.Length > i + 1)
                    {
                        if(IsValidFilename(args[i + 1]))
                        {
                            if(IsFilenameAKeyword(args[i + 1]))
                            {
                                _argData.AddToLogString($"Filename for {args[i]} cannot be a keyword.", true);
                            }
                            else
                            {
                                isFilename = true;
                                _argData.AddToLogString($"{args[i]} is a valid filename.");
                            }
                        }
                        else
                        {
                            _argData.AddToLogString($"Invalid filename for {args[i]}.", true);
                        }
                    }
                    else
                    {
                        _argData.AddToLogString($"Missing filename after {args[i]}.", true);
                    }
                }
            }

            return cleanArgs;
        }

        private void ValidateArguments()
        {
            if (_args.Count == 0)
            {
                _argData.Help = true;
                _argData.AddToLogString($"No arguments present so default: \"Help\" requested");
                _argData.NoCommands = true;
            }
            else
            {
                for (int i = 0; i < _args.Count; i++)
                {
                    if (_validNoParamOptions.Contains(_args[i]))
                    {
                        if (_args[i] == "-hr"
                            || _args[i] == "--hashrocket")
                        {
                            _argData.Debug = true;
                            _argData.AddToLogString($"\"Debug\" Mode");
                        }
                        else if (_args[i] == "-h"
                            || _args[i] == "--help")
                        {
                            _argData.Help = true;
                            _argData.AddToLogString($"\"Help\" requested");
                            _argData.NoCommands = true;
                        }
                        else if (_args[i] == "-v"
                            || _args[i] == "--version")
                        {
                            _argData.Version = true;
                            _argData.AddToLogString($"\"Version\" requested");
                            _argData.NoCommands = true;
                        }
                        else if (_args[i] == "-u"
                            || _args[i] == "--update")
                        {
                            _argData.Update = true;
                            _argData.AddToLogString($"\"Update\" requested");
                            _argData.NoCommands = true;
                        }
                        else if (_args[i] == "-d"
                            || _args[i] == "--dumplog")
                        {
                            _argData.DumpLog = true;
                            _argData.AddToLogString($"\"Dump Log\" requested");
                            _argData.NoCommands = true;
                        }
                        else if (_args[i] == "-c"
                            || _args[i] == "--clearlog")
                        {
                            _argData.ClearLog = true;
                            _argData.AddToLogString($"\"Clear Log\" requested");
                            _argData.NoCommands = true;
                        }
                        else if (_args[i] == "-l"
                            || _args[i] == "--logout")
                        {
                            _argData.Logout = true;
                            _argData.AddToLogString($"\"Logout\" requested");
                        }
                    }
                    else if (_validCommands.Contains(_args[i]))
                    {
                        if (_argData.Command == null)
                        {
                            _argData.Command = _args[i];
                            _argData.AddToLogString($"Command: \"{_args[i]}\".");
                        }
                        else
                        {
                            _argData.AddToLogString($"Only one command allowed per execution.", true);
                            _argData.AddToLogString($"   Command, \"{_argData.Command}\", already requested.");
                            _argData.AddToLogString($"   Cannot execute \"{_args[i]}\".");
                        }
                    }
                    else if (_validParamOptions.ContainsKey(_args[i]))
                    {
                        if(_args[i] == "-s" || _args[i] == "--settings")
                        {
                            ArgData.Settings = "show";
                            _argData.AddToLogString($"\"Settings\" requested");
                            _argData.NoCommands = true;
                        }

                        if(_args.Count > i + 1)
                        {
                            SavePropertyParam(_validParamOptions[_args[i]], _args[i + 1]);
                            _argData.AddToLogString($"Option: \"{_args[i]}\", Value: \"{_args[i + 1]}\".");
                            i++;
                        }
                        else 
                        {
                            if(_args[i] != "-s" && _args[i] != "--settings")
                            {
                                _argData.AddToLogString($"\"{_args[i]}\" requires an additional argument.", true);
                            }
                        }
                    }
                    else
                    {
                        if (!_argData.IsError)
                        {
                            _argData.AddToLogString($"\"{_args[i]}\" is an unknown command/option(s)", true);
                        }
                    }
                }
            }
        }

        private static void SavePropertyParam(string prop, string param)
        {
            PropertyInfo propertyInfo = _argData.GetType().GetProperty(prop);
            propertyInfo.SetValue(_argData, Convert.ChangeType(param, propertyInfo.PropertyType), null);
        }

        public bool IsValidFilename(string filename)
        {
            Regex regex = new Regex(@"^[\w\-.][\w\-./: ]*$");

            return regex.IsMatch(filename);
        }

        public bool IsFilenameAKeyword(string filename)
        {
            return _validCommands.Contains(filename)
                || _validNoParamOptions.Contains(filename)
                || _validParamOptions.ContainsKey(filename);
        }

        private readonly List<string> _validCommands = new List<string>()
        {
            "products",
            "cutchart",
            "customs"
        };
        public List<string> ValidCommands => _validCommands;

        private readonly List<string> _validNoParamOptions = new List<string>()
        {
            "-hr",
            "--hashrocket",
            "-h",
            "--help",
            "-v",
            "--version",
            "-u",
            "--update",
            "-d",
            "--dumplog",
            "-c",
            "--clearlog",
            "-l",
            "--logout"
        };
        public List<string> ValidNoParamOptions => _validNoParamOptions;

        private readonly Dictionary<string, string> _validParamOptions = new Dictionary<string, string>()
        {
            // Dict values are the property names in Args
            ["-p"] = "Product",
            ["--product"] = "Product",
            ["-o"] = "OutFile",
            ["--outfile"] = "OutFile",
            ["-x"] = "XmlFile",
            ["--xmlfile"] = "XmlFile",
            ["-u"] = "Units",
            ["--units"] = "Units",
            ["-t"] = "CcType",
            ["--type"] = "CcType",
            ["-s"] = "Settings",
            ["--settings"] = "Settings"
        };
        public Dictionary<string, string> ValidParamOptions => _validParamOptions;

        public class Args
        {
            public bool Debug { get; set; }
            public bool Help { get; set; }
            private string _helpString = @"
    usage: cc-cli.exe <command> [<args>]

            ### When used, will not execute API commands ###
                 [-h | --help] [-v | --version] [-u | --update]
                 [-s | --settings] [-d | --dumplog] [-c | --clearlog]

                    ** Settings options **
                         show
                         modify <setting> <value>

            ### Can be used with API Commands ###
                 [-p | --product]
                 [-o | --outfile] [-x | --xmlfile]
                 [-u | --units] [-t | --type]
                 [-l | --logout]
                 
            These are the available API commands:
                products        Lists the supported products.
                cutchart        Downloads cut chart data for a given product.
                customs         Downloads customized cut chart data based on an XML file.
                ";
            public string HelpString => _helpString;
            public bool Version { get; set; }
            public bool Update { get; set; }
            public string Settings { get; set; }
            public bool DumpLog { get; set; }
            public bool ClearLog { get; set; }
            public bool Logout { get; set; }
            public bool NoCommands { get; set; }
            public string Command { get; set; }
            public string Product { get; set; }
            public string OutFile { get; set; }
            public string XmlFile { get; set; }
            public string Units { get; set; }
            private string _ccType;
            public string CcType
            {
                get
                {
                    return _ccType;
                }
                set
                {
                    _ccType = value.ToUpper();
                }
            }
            private bool _isError;
            public bool IsError => _isError;
            private string _logString;
            public string LogString => _logString;

            public static implicit operator Args(string ccType)
            {
                return new Args()
                {
                    Debug = false,
                    Help = false,
                    Version = false,
                    Update = false,
                    Settings = "",
                    DumpLog = false,
                    ClearLog = false,
                    Logout = false,
                    Product = "",
                    Units = "English",
                    CcType = ccType,
                    _isError = false,
                    _logString = "\n"
                };
            }

            private PropertyInfo[] _PropertyInfos = null;
            public override string ToString()
            {
                if (_PropertyInfos == null)
                {
                    _PropertyInfos = this.GetType().GetProperties();
                }

                var sb = new StringBuilder();

                foreach (var info in _PropertyInfos)
                {
                    var value = info.GetValue(this, null) ?? "(null)";
                    sb.AppendLine(info.Name + ": " + value.ToString());
                }

                return sb.ToString();
            }

            public void AddToLogString(string message, bool isError = false)
            {
                _isError = _isError ? true : isError;
                _logString += isError ? "ERROR: " : "INFO: ";
                _logString += message + "\n";
            }
        }
    }
}
