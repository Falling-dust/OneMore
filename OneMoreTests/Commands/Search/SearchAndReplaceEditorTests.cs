//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Tests.Commands.Search
{
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using River.OneMoreAddIn.Commands;
	using River.OneMoreAddIn.Models;
	using River.OneMoreAddIn.Tests.Builders;
	using System.Linq;
	using System.Xml.Linq;


	[TestClass]
	public class SearchAndReplaceEditorTests
	{
		private static readonly XNamespace Ns =
			"http://schemas.microsoft.com/office/onenote/2013/onenote";


		[TestMethod]
		public void SearchAndReplace_ZeroWidthLinkInsertion_UsesActualInsertionNode()
		{
			var page = new Page(new PageBuilder().WithParagraph(
				"cat<a href='original'>linked</a> tail", selected: true).BuildElement());
			var editor = new SearchAndReplaceEditor("(?=cat)|(?= tail)",
				new XElement("a", new XAttribute("href", "new"), "X"), true, true);

			Assert.AreEqual(2, editor.SearchAndReplace(BodyRun(page).Parent),
				"An insertion before or after a separate link must remain replaceable.");
			var wrapper = BodyRun(page).GetCData().GetWrapper();
			Assert.AreEqual("XcatlinkedX tail", wrapper.Value);
			Assert.AreEqual(3, wrapper.Elements("a").Count());
			Assert.AreEqual(0, wrapper.Descendants("a").Descendants("a").Count());
		}


		[TestMethod]
		public void SearchAndReplace_ZeroWidthLinkInsertion_RejectsNestedLinksAtRunEnd()
		{
			var page = new Page(new PageBuilder().WithParagraph(
				"before <a href='original'><span>cat</span></a>", selected: true).BuildElement());
			var editor = new SearchAndReplaceEditor("^|(?=cat)|$",
				new XElement("a", new XAttribute("href", "new"), "X"), true, true);

			Assert.AreEqual(1, editor.SearchAndReplace(BodyRun(page).Parent));
			var wrapper = BodyRun(page).GetCData().GetWrapper();
			Assert.AreEqual("Xbefore cat", wrapper.Value);
			Assert.AreEqual(2, wrapper.Elements("a").Count());
			Assert.AreEqual(0, wrapper.Descendants("a").Descendants("a").Count());
		}


		[TestMethod]
		public void SearchAndReplace_XmlCaptureSubstitution_EscapesTextAndAttributes()
		{
			var page = new Page(new PageBuilder().WithParagraph(
				"A&amp;B&lt;3&quot;", selected: true).BuildElement());
			var editor = new SearchAndReplaceEditor("(A&B<3\")",
				new XElement("a", new XAttribute("href", "https://example.test/?q=$1"), "$1"),
				true, true);

			Assert.AreEqual(1, editor.SearchAndReplace(page));
			var anchor = BodyRun(page).GetCData().GetWrapper().Element("a");
			Assert.AreEqual("A&B<3\"", anchor.Value);
			Assert.AreEqual("https://example.test/?q=A&B<3\"", anchor.Attribute("href").Value);
		}


		[TestMethod]
		public void SearchAndReplace_LiteralReplacement_PreservesDollarAndMarkupCharacters()
		{
			var page = new Page(new PageBuilder().WithParagraph("cat", selected: true).BuildElement());
			var editor = new SearchAndReplaceEditor("cat", "$1 & <b>", false, true);

			Assert.AreEqual(1, editor.SearchAndReplace(page));
			var wrapper = BodyRun(page).GetCData().GetWrapper();
			Assert.AreEqual("$1 & <b>", wrapper.Value);
			Assert.AreEqual(0, wrapper.Elements().Count());
		}


		[DataTestMethod]
		[DataRow("\r\n")]
		[DataRow("\r")]
		[DataRow("\n")]
		public void ReplaceMatch_NewlineReplacement_PreservesLaterSnapshotOffsets(string newline)
		{
			var page = new Page(new PageBuilder().WithParagraph("cat cat", selected: true).BuildElement());
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", newline, false, true));

			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual(2, session.Current.StartIndex);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("\n \n", BodyRun(page).GetCData().GetWrapper().Value);
			Assert.AreEqual(0, session.RemainingCount);
		}


		[TestMethod]
		public void ReplaceMatch_XmlNewlineReplacement_PreservesLaterSnapshotOffsets()
		{
			var page = new Page(new PageBuilder().WithParagraph("cat cat", selected: true).BuildElement());
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", new XElement("span", "\r\n"), false, true));

			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual(2, session.Current.StartIndex);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("\n \n", BodyRun(page).GetCData().GetWrapper().Value);
			Assert.AreEqual(0, session.RemainingCount);
		}


		private static XElement BodyRun(Page page) =>
			page.Root.Elements(Ns + "Outline").Descendants(Ns + "T").First();


		// Reproduces GitHub #1683 / #2528: a hashtag styled by a prior scan (or wrapped in a
		// link, as originally reported) leaves a T run whose top-level node is a SPAN/anchor
		// containing more than one child node. ElementAtom.Length reports the length of the
		// element's full concatenated text, but Replace() used to delegate straight to the
		// first child node's own (shorter) text, causing TextAtom.Replace to Substring() out
		// of range and crash the hashtag service.
		[TestMethod]
		public void SearchAndReplace_HashtagsInsideAnchorWithMultipleSpans_DoesNotThrow()
		{
			var replacement = new XElement("span",
				new XAttribute("style", "color:red"),
				"$1");

			var editor = new SearchAndReplaceEditor(@"(#\w+)", replacement, true, false);

			var t = new XElement(Ns + "T", new XCData(
				"<a href=\"c:\">aaa " +
				"<span style='background:#FFFF99'>#abb</span> " +
				"<span style='background:#FFFF99'>#acc</span></a>"));

			var oe = new XElement(Ns + "OE",
				new XAttribute("objectID", "{OE1}"),
				new XAttribute("lastModifiedTime", "2026-01-01T00:00:00.000Z"),
				t);

			var page = new PageBuilder().WithElement(oe).BuildElement();
			var paragraph = page.Elements(Ns + "Outline").Descendants(Ns + "OE").First();

			var count = editor.SearchAndReplace(paragraph);

			Assert.AreEqual(2, count);

			var text = paragraph.Element(Ns + "T").GetCData().Value;
			Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(
				text, "color:red").Count);
		}


		// Reproduces GitHub #1425: mathML equations are stored as a one:T whose CDATA is
		// entirely an XML comment. XElement.Value skips comment content, so a wildcard regex
		// (e.g. ".*") that legitimately matches everywhere else also produces a zero-length
		// match against the comment's empty rawtext, which used to flow into Replace() and
		// NullReferenceException inside AtomicFactory/ElementAtom. The mathML run must be
		// skipped entirely and left byte-for-byte untouched.
		[TestMethod]
		public void SearchAndReplace_MathMLEquation_IsNotModified()
		{
			const string mathml =
				"<!--[if mathML]><math xmlns=\"http://www.w3.org/1998/Math/MathML\" " +
				"display=\"block\"><mi>A</mi><mo>=</mo><mi>&#120587;</mi><msup><mi>r</mi>" +
				"<mn>2</mn></msup></math><![endif]-->";

			var mathT = new XElement(Ns + "T", new XCData(mathml));
			var textT = new XElement(Ns + "T", new XCData("hello world"));

			var oe = new XElement(Ns + "OE",
				new XAttribute("objectID", "{OE1}"),
				new XAttribute("lastModifiedTime", "2026-01-01T00:00:00.000Z"),
				mathT,
				textT);

			var page = new PageBuilder().WithElement(oe).BuildElement();
			var paragraph = page.Elements(Ns + "Outline").Descendants(Ns + "OE").First();

			// wildcard regex matches (and would otherwise zero-length-match the empty
			// rawtext of the mathML comment)
			var editor = new SearchAndReplaceEditor(@".*", "X", true, false);

			editor.SearchAndReplace(paragraph);

			var runs = paragraph.Elements(Ns + "T").ToList();
			Assert.AreEqual(mathml, runs[0].GetCData().Value,
				"mathML CDATA must remain byte-for-byte unchanged");
			Assert.AreNotEqual("hello world", runs[1].GetCData().Value,
				"plain text run should still have been replaced");
		}
	}
}
