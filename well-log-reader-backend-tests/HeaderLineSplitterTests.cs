using NUnit.Framework;
using well_log_reader_backend;

namespace well_log_reader_backend_tests
{
    [TestFixture]
    public class HeaderLineSplitterTests
    {
        [Test]
        public void NormalLine_IsSplitIntoFourParts()
        {
            Header h = Split(" STRT.M        1670.0000 : START DEPTH", 12);

            Assert.That(h.mnem, Is.EqualTo("STRT"));
            Assert.That(h.unit, Is.EqualTo("M"));
            Assert.That(h.value, Is.EqualTo("1670.0000"));
            Assert.That(h.description, Is.EqualTo("START DEPTH"));
            Assert.That(h.line, Is.EqualTo(12));
        }

        [Test]
        public void NoUnit_GivesEmptyUnit()
        {
            Header h = Split(" WELL.   ANY WELL #1 : WELL NAME");

            Assert.That(h.mnem, Is.EqualTo("WELL"));
            Assert.That(h.unit, Is.EqualTo(""));
            Assert.That(h.value, Is.EqualTo("ANY WELL #1"));
            Assert.That(h.description, Is.EqualTo("WELL NAME"));
        }

        [Test]
        public void EmptyValue_GivesEmptyValue()
        {
            Header h = Split(" UWI .                 : UNIQUE WELL ID");

            Assert.That(h.mnem, Is.EqualTo("UWI"));
            Assert.That(h.unit, Is.EqualTo(""));
            Assert.That(h.value, Is.EqualTo(""));
            Assert.That(h.description, Is.EqualTo("UNIQUE WELL ID"));
        }

        [Test]
        public void ValueContainingDots_OnlyFirstDotEndsMnemonic()
        {
            Header h = Split(" DATE.   2020.01.15 : LOG DATE");

            Assert.That(h.mnem, Is.EqualTo("DATE"));
            Assert.That(h.unit, Is.EqualTo(""));
            Assert.That(h.value, Is.EqualTo("2020.01.15"));
        }

        [Test]
        public void TimeValueContainingColons_LastColonSeparatesDescription()
        {
            Header h = Split(" TIME.   13:45:10 : TIME LOGGED");

            Assert.That(h.value, Is.EqualTo("13:45:10"));
            Assert.That(h.description, Is.EqualTo("TIME LOGGED"));
        }

        [Test]
        public void UnitWithoutSpaceBeforeColon_StopsAtColon()
        {
            Header h = Split("GR.GAPI:Gamma Ray");

            Assert.That(h.mnem, Is.EqualTo("GR"));
            Assert.That(h.unit, Is.EqualTo("GAPI"));
            Assert.That(h.value, Is.EqualTo(""));
            Assert.That(h.description, Is.EqualTo("Gamma Ray"));
        }

        [Test]
        public void TabAfterUnit_CountsAsSpace()
        {
            Header h = Split("NULL.\t-999.25\t: NULL VALUE");

            Assert.That(h.unit, Is.EqualTo(""));
            Assert.That(h.value, Is.EqualTo("-999.25"));
        }

        [TestCase("GR  .GAPI", "No colon; line skipped")]
        [TestCase("WELL   SMITH #12 : WELL NAME", "No dot; line skipped")]
        [TestCase("WELL   SMITH #12   WELL NAME", "No dot or colon; line skipped")]
        [TestCase("NOTE : see report v2.1", "First dot comes after the last colon; line skipped")]
        [TestCase("   .M   100 : NO MNEMONIC", "No mnemonic before the dot; line skipped")]
        [TestCase("", "No dot or colon; line skipped")]
        public void LineThatCannotBeSplit_IsSkippedWithReason(string text, string expectedError)
        {
            Header header;
            string error;
            bool ok = HeaderLineSplitter.TrySplit(text, 1, out header, out error);

            Assert.That(ok, Is.False);
            Assert.That(header, Is.Null);
            Assert.That(error, Is.EqualTo(expectedError));
        }

        [Test]
        public void NullText_DoesNotThrow()
        {
            Header header;
            string error;
            bool ok = HeaderLineSplitter.TrySplit(null, 1, out header, out error);

            Assert.That(ok, Is.False);
        }

        private static Header Split(string text, int lineNumber = 1)
        {
            Header header;
            string error;
            bool ok = HeaderLineSplitter.TrySplit(text, lineNumber, out header, out error);

            Assert.That(ok, Is.True, error);
            return header;
        }
    }
}
