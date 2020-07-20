using FluentAssertions;
using NUnit.Framework;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Hypertherm.CcCli.Mocks;
using System;
using static Hypertherm.Logging.LoggingService;
using Hypertherm.Logging;
using Moq;
using Moq.Protected;
using Hypertherm.Analytics;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;

namespace Hypertherm.CcCli.Tests
{
    [TestFixture]
    [Ignore("Waiting api endpoint", Until = "2020-01-31 12:00:00Z")]
    public class CcApiServiceTests
    {
        [Test]
        ///<summary>
        /// As all requests to the API use the same HTTP client.
        /// Therefore if we test one of the API interactions we test all of them.
        /// </summary>
        public void IsBaseCcApiEndpointAvailable()
        {
          Mock<HttpMessageHandler> mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new SocketException())
                .ReturnsAsync(new HttpResponseMessage()
                  {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                  }
                )
                .ReturnsAsync(new HttpResponseMessage()
                  {
                    StatusCode = HttpStatusCode.Ambiguous,
                  }
                )
                .ReturnsAsync(new HttpResponseMessage()
                  {
                    StatusCode = HttpStatusCode.OK
                  }
                );
            
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAnalyticsService>(Mock.Of<IAnalyticsService>());
            serviceCollection.AddSingleton<ILoggingService>(Mock.Of<ILoggingService>());
            ApplicationServiceProvider.AddApiService(mockHandler.Object, serviceCollection);
            ServiceProvider provider = serviceCollection.BuildServiceProvider();
            CcApiService service = (CcApiService) provider.GetRequiredService<IApiService>();
            var status = service.IsCcApiAvailable().Result;

            status.Should().Be(HttpStatusCode.OK, "CutChart API is currently unavailable.");
        }

       [Test]
        public void ValidProductNameExists()
        {
            CcApiService _ccApiService = CreateMockCcApiService();
            List<string> _productNames = new List<string>(){"powermax105", "maxpro200"};
            var productNames = _ccApiService.GetProductNames().Result;

            Assert.True(productNames.Contains(_productNames[0]), _productNames[0] + " product does not exist.");
        }

        [Test]
        public void InvalidOrMissingFilenameGetBaseCutChartData()
        {
            CcApiService _ccApiService = CreateMockCcApiService();
            List<string> _productNames = new List<string>(){"powermax105", "maxpro200"};
            string outputXlsxFilename = "";
            
            Action A = () => 
                _ccApiService.GetBaseCutChartData(outputXlsxFilename, _productNames[0], "english", "XLSX")
                    .GetAwaiter().GetResult();

            A.Should().NotThrow();
        }

        [Test]
        public void ValidProductCutChartDataAsXlsx()
        {
            CcApiService _ccApiService = CreateMockCcApiService();
            List<string> _productNames = new List<string>(){"powermax105", "maxpro200"};
            string outputXlsxFilename = "./cutcharts.xlsx";

            Action A = () =>
                _ccApiService.GetBaseCutChartData(outputXlsxFilename, _productNames[0], "english", "XLSX")
                    .GetAwaiter().GetResult();

            A.Should().NotThrow();
            FileAssert.Exists(outputXlsxFilename);
            File.Delete(outputXlsxFilename);
        }

        [Test]
        public void AllBaseCutChartDataAsDb()
        {
            CcApiService _ccApiService = CreateMockCcApiService();
            List<string> _productNames = new List<string>(){"powermax105", "maxpro200"};
            string outputDbFilename = "./cutcharts.db";

            Action A = () =>
                _ccApiService.GetAllCutChartData(outputDbFilename, "DB").GetAwaiter().GetResult();

            A.Should().NotThrow();
            FileAssert.Exists(outputDbFilename);
            File.Delete(outputDbFilename);
        }

        [Test]
        public void CanGetXmlTransformedCutChartsAsXlsx()
        {
            CcApiService _ccApiService = CreateMockCcApiService();
            List<string> _productNames = new List<string>(){"powermax105", "maxpro200"};
            string outputXlsxFilename = "./customcutcharts.xlsx";

            var xmlFilename = "./testXml.xml";
            var xmlFileStream = System.IO.File.Create(xmlFilename);
            var xmlWriter = new System.IO.StreamWriter(xmlFileStream);
            xmlWriter.WriteLine(_testXmlTransform);
            xmlWriter.Dispose();

            FileAssert.Exists(xmlFilename);

            Action A = () =>
                _ccApiService.GetXmlTransformedCutChartData(outputXlsxFilename, xmlFilename, _productNames[1])
                    .GetAwaiter().GetResult();

            A.Should().NotThrow();
            FileAssert.Exists(outputXlsxFilename);
            File.Delete(outputXlsxFilename);
            File.Delete(xmlFilename);
        }

        [Test]
        public void UriWithQueryParams()
        {
            CcApiService ccApiService = CreateMockCcApiService();

            var testUri = CcApiUtilities.BuildUrl(new[] { "base", "route" }, new Dictionary<string, string>() {
                { "units", "english" },
                { "param2", "valid"}
                 });
            Assert.AreEqual("/cutchart/base/route?units=english&param2=valid", testUri.PathAndQuery);
        }
        
        private CcApiService CreateMockCcApiService(bool reset = false)
        {
            HttpClient client = new HttpClient(HttpTestUtilities.CreateMockClientHandler());

            var logger = new LoggingService(MessageType.Error);
            return new CcApiService(
              analyticsService:Mock.Of<IAnalyticsService>(),
              logger:logger,
              client:client
            );
        }

        string _testXmlTransform = @"<?xml version='1.0' encoding='utf-8'?>
<Document units='English' productfamily='88' cutcharts='N'>
  <Column name='cut_chart_type' omit='true'/><!--omit an existing column-->
  <Column name='kerf'><!--modifying existing column-->
    <Cell color='255,0,0'/><!--sets background color of all cells in the column, columnname is not required because the name of the column element is automatically used-->
  </Column>
  <Column name='profile_area' index='I'/><!--move column, index accepts excel letter index or 0 based integer index-->
  <Column name='empty_column' index='4'/><!--index is not reccommended because it is only honored for initial order, this pushes profile_area to index='J'-->
  <Column name='diameter_feedrate' before='empty_column'><!--this pushes empty_column to index='5' and profile_area to index='K'-->
    <Cell formula='true'>{diameter}{row}*{base_feedrate}{row}</Cell><!--accepts normal excel syntax except that columns need to be called out by name in {column_name} and current row number by {row}-->
  </Column>
  <Column name='arbitrary_value'><!--creates a new column that will default to being the last column-->
    <Cell>25</Cell><!--constant value for the whole column-->
  </Column>
  <Record material='MS' thickness='*' class='65A Shielded Air'><!--select oracle record/s, oracle recordss won't show up in excell unless they are selected and have a row generated from them-->
    <Row profile_type='*' diameter='*' feedrate_pct='81'/><!--generates a row based on the selected oracle record/s-->
    <Row profile_type='I' diameter='*' feedrate_pct='74'/>
  </Record>
  <Record material='MS' thickness='*' class='105A SYNC Air'>
    <Row profile_type='*' diameter='*' feedrate_pct='100'/>
    <Row profile_type='I' diameter='*' feedrate_pct='100'/>
    <Row profile_type='H' diameter='2' feedrate_pct='100'>
      <Cell columnname='feedrate_pct' color='178,173,127' 
      note='This value is for reference only. It is not used during output for True Hole. AHC is off'/><!--sets the background color and adds a note to the cell in this row-->
    </Row>
    <Row profile_type='H' diameter='1' feedrate_pct='100'>
      <Cell columnname='feedrate_pct' color='178,173,127' note='This value is for reference only. It is not used during output for True Hole. AHC is off'/>
    </Row>
  </Record>
  <Record material='MS' thickness='*' class='85A Shielded Air'>
    <Row profile_type='*' diameter='*' feedrate_pct='100'/>
    <Row profile_type='I' diameter='5' feedrate_pct='80'/>
    <Row profile_type='I' diameter='3' feedrate_pct='60'/>
    <Row profile_type='I' diameter='1' feedrate_pct='40'/>
  </Record>
  <Record material='*' thickness='*' class='*'><!--selects all oracle records that havent been selected yet-->
    <Row profile_type='*' diameter='*' feedrate_pct='100'/>
    <Row profile_type='I' diameter='5' feedrate_pct='80'/>
    <Row profile_type='I' diameter='3' feedrate_pct='60' disable_ahc='true'/>
    <Row profile_type='I' diameter='1' feedrate_pct='40' disable_ahc='true'/>
  </Record>
</Document>";
    }
}