using NUnit.Framework;

namespace Hypertherm.CcCli.Tests
{
    [TestFixture]
    public class ArgumentHandlerTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void NoArguments()
        {
            string[] args = { };
            ArgumentHandler argHandler = new ArgumentHandler(args);

            // All three of these do the same thing. "True()" being the newest, That() being the method they are overloading
            // Assert.That(argHandler.HasValidArguments(), Is.True, "Zero arguments is returning invalid");
            // Assert.IsTrue(argHandler.HasValidArguments(), "Zero arguments is returning invalid");
            Assert.True(argHandler.HasValidArguments(), "Zero arguments is returning invalid");

            Assert.AreEqual("", argHandler.ArgData.Product, "The Product property should be an empty string");
            Assert.AreEqual(null, argHandler.ArgData.OutFile, "The OutFile property should be an empty string");
            Assert.AreEqual(null, argHandler.ArgData.XmlFile, "The XmlFile property should be an empty string");
            Assert.AreEqual("XLSX", argHandler.ArgData.CcType, "The CcType property should default to \"XLSX\"");
        }

        [Test]
        public void TestCommandsMap()
        {
            string[] args = { "Products" };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The products command is returning invalid");
            Assert.AreEqual("products", argHandler.ArgData.Command, $"The Command property should be \"products\"");

            args = new string[] { "cutchart" };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The CutChart command is returning invalid");
            Assert.AreEqual("cutchart", argHandler.ArgData.Command, $"The Command property should be \"cutchart\"");

            args = new string[] { "CUSTOMS" };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The Customs command is returning invalid");
            Assert.AreEqual("customs", argHandler.ArgData.Command, $"The Command property should be \"customs\"");
        }

        [Test]
        public void HelpOptionsAreValid()
        {
            string[] args = { "-h" };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -h option returning invalid");
            Assert.True(argHandler.ArgData.Help, "The Help flag is not getting set");

            args = new string[] { "--hElP" };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The --help option is returning invalid");
            Assert.True(argHandler.ArgData.Help, "The Help flag is not getting set");
        }

        [Test]
        public void ValidateHelpString()
        {
            //Make sure all options are present in the help string.
            string[] args = { "-h" };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            foreach (var command in argHandler.ValidCommands)
            {
                Assert.True(argHandler.ArgData.HelpString.Contains(command),
                        $"The help string does not contain {command}.");
            }
            foreach (var option in argHandler.ValidNoParamOptions)
            {
                if (option != "-hr" && option != "--hashrocket")
                {
                    Assert.True(argHandler.ArgData.HelpString.Contains(option),
                            $"The help string does not contain {option}.");
                }
            }
            foreach (var option in argHandler.ValidParamOptions)
            {
                Assert.True(argHandler.ArgData.HelpString.Contains(option.Key),
                        $"The help string does not contain {option.Key}.");
            }
        }

        [Test]
        public void VersionOptionsAreValid()
        {
            string[] args = { "-v" };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -v option returning invalid");
            Assert.True(argHandler.ArgData.Version, "The Version flag is not getting set");

            args = new string[] { "--VERSION" };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The --version option is returning invalid");
            Assert.True(argHandler.ArgData.Version, "The Version flag is not getting set");
        }

        [Test]
        public void ProductOptionsAreValid()
        {
            string productName = "Powermax105";
            string[] args = { "-p", productName };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -p option returning invalid");
            Assert.AreEqual(productName.ToLower(), argHandler.ArgData.Product, $"The Product property should be \"{productName.ToLower()}\"");

            args = new string[] { "--PRODUCT", productName };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The --product option is returning invalid");
            Assert.AreEqual(productName.ToLower(), argHandler.ArgData.Product, $"The Product property should be \"{productName.ToLower()}\"");
        }

        // TODO: Test that arguments following filename options look like filenames not other options or commands
        [Test]
        public void FilenameOptionsAreValid()
        {
            string outfileDB = "./Output.db";
            string[] args = new string[] { "-o", outfileDB };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -o option is returning invalid");
            Assert.AreEqual(outfileDB, argHandler.ArgData.OutFile, $"The OutFile property should be \"{outfileDB}\"");

            string outfileXLSX = "./Output.xlsx";
            args = new string[] { "--outfile", outfileXLSX };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -outfile option is returning invalid");
            Assert.AreEqual(outfileXLSX, argHandler.ArgData.OutFile, $"The OutFile property should be \"{outfileXLSX}\"");

            string xmlfile = "J:/tmp/Transform.xml";
            args = new string[] { "-x", xmlfile };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -x option is returning invalid");
            Assert.AreEqual(xmlfile, argHandler.ArgData.XmlFile, $"The XmlFile property should be \"{xmlfile}\"");

            args = new string[] { "--xmlfile", xmlfile };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -xmlfile option is returning invalid");
            Assert.AreEqual(xmlfile, argHandler.ArgData.XmlFile, $"The XmlFile property should be \"{xmlfile}\"");
        }

        [Test]
        public void TheseFilenamesAreBad()
        {
            string[] args = new string[] { "-o", "--help" };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            Assert.False(argHandler.HasValidArguments(), "The --help should be an invalid filename");
        }

        [Test]
        public void FileTypeOptionsAreValid()
        {
            // XLSX is assumed by default

            string[] args = new string[] { "-t", "db" };

            ArgumentHandler argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -t option is returning invalid");
            Assert.AreEqual("DB", argHandler.ArgData.CcType, "The CcType property should default to \"DB\"");

            args = new string[] { "--type", "XlSx" };

            argHandler = new ArgumentHandler(args);
            Assert.True(argHandler.HasValidArguments(), "The -type option is returning invalid");
            Assert.AreEqual("XLSX", argHandler.ArgData.CcType, "The CcType property should default to \"XLSX\"");
        }

        // TODO Test errors via LogString
        // Add checks to the failure tests, to confirm error(s) in the LogString
    }
}