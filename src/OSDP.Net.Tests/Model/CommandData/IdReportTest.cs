using NUnit.Framework;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData
{
    [TestFixture]
    [Category("Unit")]
    public class IdReportTest
    {
        [Test]
        public void DefaultConstructor_RequestsStandardId()
        {
            var idReport = new IdReport();

            Assert.That(idReport.RequestExtended, Is.False);
            var data = idReport.BuildData();
            Assert.That(data[0], Is.EqualTo(0x00));
        }

        [Test]
        public void Constructor_RequestExtendedFalse_BuildsStandardRequest()
        {
            var idReport = new IdReport(requestExtended: false);

            Assert.That(idReport.RequestExtended, Is.False);
            var data = idReport.BuildData();
            Assert.That(data.Length, Is.EqualTo(1));
            Assert.That(data[0], Is.EqualTo(0x00));
        }

        [Test]
        public void Constructor_RequestExtendedTrue_BuildsExtendedRequest()
        {
            var idReport = new IdReport(requestExtended: true);

            Assert.That(idReport.RequestExtended, Is.True);
            var data = idReport.BuildData();
            Assert.That(data.Length, Is.EqualTo(1));
            Assert.That(data[0], Is.EqualTo(0x01));
        }

        [Test]
        public void CommandType_ReturnsIdReport()
        {
            var idReport = new IdReport();

            Assert.That(idReport.CommandType, Is.EqualTo(OSDP.Net.Messages.CommandType.IdReport));
        }

        [Test]
        public void Code_ReturnsCorrectValue()
        {
            var idReport = new IdReport();

            Assert.That(idReport.Code, Is.EqualTo(0x61));
        }
    }
}
