using NUnit.Framework;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Utilities;
using System;
using System.Linq;

namespace OSDP.Net.Tests.Model.CommandData
{
    [Category("Unit")]
    public class OutputControlTest
    {
        [Test]
        public void BuildData()
        {
            var actual = new OutputControl(5, OutputControlCode.PermanentStateOnAllowTimedOperation, 10000).BuildData().ToArray();

            Assert.That(BitConverter.ToString(actual), Is.EqualTo("05-04-10-27"));
        }

        [Test]
        public void ParseData()
        {
            var inputData = BinaryUtils.HexToBytes("05-04-10-27").ToArray();
            var actual = OutputControl.ParseData(inputData);

            Assert.That(actual.OutputNumber, Is.EqualTo(5));
            Assert.That(actual.OutputControlCode, Is.EqualTo(OutputControlCode.PermanentStateOnAllowTimedOperation));
            Assert.That(actual.Timer, Is.EqualTo(10000));
        }
    }
}
