//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Tests.Commands.Search
{
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using River.OneMoreAddIn.Commands;
	using River.OneMoreAddIn.Models;
	using River.OneMoreAddIn.Tests.Builders;
	using System;
	using System.Linq;
	using System.Xml.Linq;


	[TestClass]
	public class SearchAndReplaceSessionTests
	{
		private static readonly XNamespace Ns =
			"http://schemas.microsoft.com/office/onenote/2013/onenote";


		[DataTestMethod]
		[DataRow("x", "x x x")]
		[DataRow("", "  ")]
		[DataRow("longer", "longer longer longer")]
		[DataRow("cat", "cat cat cat")]
		[DataRow("catcat", "catcat catcat catcat")]
		public void ReplaceCurrent_LengthChangesAndSelfMatches_VisitEachOriginalOnce(
			string replacement, string expected)
		{
			var page = CreatePage("cat cat cat");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", replacement, false, true));

			for (var i = 0; i < 3; i++)
			{
				Assert.AreEqual(i * (replacement.Length + 1), session.Current.StartIndex);
				Assert.AreEqual("cat", session.Current.MatchedText);
				Assert.IsTrue(session.ReplaceCurrent());
			}

			Assert.AreEqual(expected, BodyText(page));
			Assert.AreEqual(3, session.ReplacedCount);
			Assert.AreEqual(0, session.RemainingCount);
			Assert.IsNull(session.Current);
			Assert.IsFalse(session.ReplaceCurrent());
			Assert.AreEqual(0, session.ReplaceAll());
		}


		[TestMethod]
		public void ReplaceAll_SkippedAndVisitedMatches_ReplacesAllTextboxesAndTables()
		{
			var root = new PageBuilder().WithElement(Paragraph("first", "cat cat")).BuildElement();
			root.Add(new XElement(Ns + "Outline", new XElement(Ns + "OEChildren",
				Paragraph("second", "cat"),
				new XElement(Ns + "OE", new XElement(Ns + "Table",
					new XElement(Ns + "Row", new XElement(Ns + "Cell",
						new XElement(Ns + "OEChildren", Paragraph("cell", "cat cat")))))))));
			var page = new Page(root);
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "lion", false, true));

			Assert.AreEqual(5, session.Matches.Count);
			Assert.IsTrue(session.MoveNext(false));
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("second", session.Current.ParagraphObjectId);
			Assert.AreEqual(4, session.ReplaceAll());
			Assert.AreEqual("lion lion|lion|lion lion", BodyText(page));
			Assert.AreEqual(0, session.RemainingCount);
		}


		[TestMethod]
		public void ReplaceCurrent_AfterSavingFirstTextbox_RetainsLaterTextbox()
		{
			var root = new PageBuilder().WithElement(Paragraph("first", "cat")).BuildElement();
			root.Add(new XElement(Ns + "Outline", new XElement(Ns + "OEChildren",
				Paragraph("second", "cat"))));
			var page = new Page(root);
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "x", false, true));

			Assert.IsTrue(session.ReplaceCurrent());
			SearchAndReplacePageState.CreateSaveCopy(page).OptimizeForSave(false);
			Assert.AreEqual("second", session.Current.ParagraphObjectId);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("x|x", BodyText(page));
		}


		[TestMethod]
		public void Navigation_WrapAndBoundaries_KeepPendingMatchesReachable()
		{
			var session = new SearchAndReplaceSession(CreatePage("cat cat cat"),
				new SearchAndReplaceEditor("cat", "x", false, true));

			Assert.IsFalse(session.MovePrevious(false));
			Assert.AreEqual(0, session.CurrentIndex);
			Assert.IsTrue(session.MovePrevious(true));
			Assert.AreEqual(2, session.CurrentIndex);
			Assert.IsFalse(session.MoveNext(false));
			Assert.AreEqual(2, session.CurrentIndex);
			Assert.IsTrue(session.ReplaceCurrent(false));
			Assert.IsNull(session.Current);
			Assert.AreEqual(2, session.RemainingCount);
			Assert.IsTrue(session.MovePrevious(false));
			Assert.AreEqual(1, session.CurrentIndex);
			Assert.IsTrue(session.ReplaceCurrent(true));
			Assert.AreEqual(0, session.CurrentIndex);
			Assert.IsTrue(session.ReplaceCurrent(true));
			Assert.IsFalse(session.MoveNext(true));
			Assert.IsFalse(session.MovePrevious(true));
		}


		[TestMethod]
		public void Rescan_RetainsOriginalSelection_AndExplicitlyFindsNewMatches()
		{
			var page = new Page(new PageBuilder()
				.WithParagraph("cat", selected: true)
				.WithParagraph("cat", selected: true)
				.WithParagraph("cat", selected: false).BuildElement());
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "catcat", false, true));
			Assert.AreEqual(2, session.ReplaceAll());
			Assert.AreEqual(0, session.RemainingCount);

			page.Root.Descendants().Attributes("selected").Remove();
			page.Root.Descendants(Ns + "T").Last().SetAttributeValue("selected", "all");
			session.Rescan();

			Assert.AreEqual(4, session.RemainingCount);
			Assert.AreEqual(0, session.ReplacedCount);
			Assert.AreEqual(4, session.ReplaceAll());
			Assert.AreEqual("catcatcatcat|catcatcatcat|cat", BodyText(page));
		}


		[TestMethod]
		public void ReplaceCurrent_ZeroWidthInsertion_ShiftsPendingMatchesOnce()
		{
			var page = CreatePage("aa");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("(?=a)|$", "a", true, true));

			Assert.AreEqual(3, session.RemainingCount);
			Assert.AreEqual(0, session.Current.Length);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual(2, session.Current.StartIndex);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual(4, session.Current.StartIndex);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("aaaaa", BodyText(page));
			Assert.AreEqual(0, session.RemainingCount);
		}


		[TestMethod]
		public void ReplaceAll_ZeroWidthAndNonemptyMatchesAtBoundary_UsesSnapshotOrder()
		{
			var page = CreatePage("ab");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("a*", "x", true, true));

			Assert.AreEqual(3, session.ReplaceAll());
			Assert.AreEqual("xxbx", BodyText(page));
		}


		[TestMethod]
		public void ReplaceCurrent_RegexContextAndCaptures_AreExpandedBeforeAnyEdits()
		{
			var page = CreatePage("cat:1 cat:22");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor(@"(cat):(\d+)", "$2", true, true));

			Assert.AreEqual("cat", session.Current.MatchedText);
			Assert.AreEqual("1", session.Current.ReplacementText);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual(4, session.Current.StartIndex);
			Assert.AreEqual("22", session.Current.ReplacementText);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("1:1 22:22", BodyText(page));
		}


		[TestMethod]
		public void ReplaceCurrent_FormattedNodesAndRuns_PreservesUnrelatedMarkup()
		{
			var page = CreatePage("<span style='font-weight:bold'>c</span>" +
				"<span style='color:red'>at cat</span><span style='color:blue'> tail</span>");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "fox", false, true));

			Assert.IsTrue(session.ReplaceCurrent());
			Assert.IsTrue(session.ReplaceCurrent());
			var wrapper = page.Root.Elements(Ns + "Outline").Descendants(Ns + "T")
				.First().GetCData().GetWrapper();
			Assert.AreEqual("fox fox tail", wrapper.Value);
			Assert.AreEqual("fox", wrapper.Elements("span").First().Value);
			Assert.AreEqual(" tail", wrapper.Elements("span").Last().Value);
			Assert.AreEqual("color:blue", wrapper.Elements("span").Last().Attribute("style").Value);
		}


		[TestMethod]
		public void ReplaceCurrent_XmlReplacement_UsesVisibleLengthForLaterOffsets()
		{
			var page = CreatePage("cat cat");
			var replacement = new XElement("a", new XAttribute("href", "https://example.com"), "kitten");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", replacement, false, true));

			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual(7, session.Current.StartIndex);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("kitten kitten", BodyText(page));
			Assert.AreEqual(2, page.Root.Elements(Ns + "Outline").Descendants(Ns + "T")
				.First().GetCData().GetWrapper().Elements("a").Count());
		}


		[TestMethod]
		public void Checkpoint_FailedSave_RestoresTextOffsetsAndNavigation()
		{
			var page = CreatePage("cat cat cat");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "kitten", false, true));
			session.MoveNext(false);
			var original = session.Current;
			var checkpoint = session.CreateCheckpoint();
			session.ReplaceCurrent();
			session.ReplaceAll();
			session.RestoreCheckpoint(checkpoint);

			Assert.AreEqual("cat cat cat", BodyText(page));
			Assert.AreSame(original, session.Current);
			Assert.AreEqual(4, session.Current.StartIndex);
			Assert.AreEqual(8, session.Matches[2].StartIndex);
			Assert.AreEqual(3, session.RemainingCount);
			Assert.AreEqual(0, session.ReplacedCount);
			Assert.AreEqual(3, session.ReplaceAll());
			Assert.AreEqual("kitten kitten kitten", BodyText(page));
		}


		[TestMethod]
		public void ReplaceCurrent_StaleText_FailsWithoutChangingPageOrAdvancing()
		{
			var page = CreatePage("cat cat");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "dog", false, true));
			session.Current.TElement.GetCData().Value = "bat cat";

			Assert.ThrowsException<InvalidOperationException>(() => session.ReplaceCurrent());
			Assert.AreEqual("bat cat", BodyText(page));
			Assert.AreEqual(0, session.CurrentIndex);
			Assert.AreEqual(2, session.RemainingCount);
		}


		[TestMethod]
		public void TryRebase_FreshFormattingAndNontext_SurviveNextReplacement()
		{
			var page = CreatePage("cat cat");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "fox", false, true));
			Assert.IsTrue(session.ReplaceCurrent());
			var fresh = SearchAndReplacePageState.CreateSaveCopy(page);
			var run = fresh.Root.Elements(Ns + "Outline").Descendants(Ns + "T").Single();
			run.GetCData().Value = "<span style='color:blue'>fox cat</span>";
			var image = new XElement(Ns + "Image", new XAttribute("format", "png"),
				new XElement(Ns + "Data", "updated-image-data"));
			run.Parent.Add(image);
			fresh.Root.SetAttributeValue("pageColor", "#123456");

			Assert.IsTrue(session.TryRebase(fresh));
			Assert.AreSame(run, session.Current.TElement);
			Assert.IsTrue(session.ReplaceCurrent());

			var saved = SearchAndReplacePageState.CreateSaveCopy(fresh);
			var span = saved.Root.Elements(Ns + "Outline").Descendants(Ns + "T")
				.Single().GetCData().GetWrapper().Element("span");
			Assert.AreEqual("fox fox", span.Value);
			Assert.AreEqual("color:blue", span.Attribute("style").Value);
			Assert.IsTrue(XNode.DeepEquals(image, saved.Root.Descendants(Ns + "Image").Single()));
			Assert.AreEqual("#123456", saved.Root.Attribute("pageColor").Value);
			Assert.AreEqual("fox cat", BodyText(page));
		}


		[TestMethod]
		public void TryRebase_MergedRuns_RemapsOffsetsAndRetainsSnapshotCaptures()
		{
			var paragraph = Paragraph("body", "cat:1 ");
			paragraph.Add(new XElement(Ns + "T", new XCData("cat:22")));
			var page = new Page(new PageBuilder().WithElement(paragraph).BuildElement());
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor(@"(cat):(\d+)", "$2", true, true));
			var first = session.Current;
			Assert.IsTrue(session.ReplaceCurrent());
			var pending = session.Current;
			var fresh = SearchAndReplacePageState.CreateSaveCopy(page);
			var freshParagraph = fresh.Root.Descendants(Ns + "OE")
				.Single(oe => (string)oe.Attribute("objectID") == "body");
			freshParagraph.Elements(Ns + "T").Remove();
			var merged = new XElement(Ns + "T", new XCData("1:1 cat:22"));
			freshParagraph.Add(merged);

			Assert.IsTrue(session.TryRebase(fresh));
			Assert.AreEqual(2, session.Matches.Count);
			Assert.AreSame(first, session.Matches[0]);
			Assert.IsTrue(first.IsReplaced);
			Assert.AreEqual("1", first.ReplacementText);
			Assert.AreSame(pending, session.Current);
			Assert.AreSame(merged, pending.TElement);
			Assert.AreEqual(4, pending.StartIndex);
			Assert.AreEqual("cat", pending.MatchedText);
			Assert.AreEqual("22", pending.ReplacementText);
			Assert.AreEqual(1, session.RemainingCount);
			Assert.IsTrue(session.ReplaceCurrent());
			Assert.AreEqual("1:1 22:22", BodyText(fresh));
			Assert.AreEqual(2, session.ReplacedCount);
		}


		[TestMethod]
		public void TryRebase_SplitBetweenMatches_RemapsBothRuns()
		{
			var page = CreatePage("cat cat");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "fox", false, true));
			var fresh = SearchAndReplacePageState.CreateSaveCopy(page);
			var original = fresh.Root.Elements(Ns + "Outline").Descendants(Ns + "T").Single();
			var first = new XElement(Ns + "T", new XCData("cat "));
			var second = new XElement(Ns + "T", new XCData("cat"));
			original.ReplaceWith(first, second);

			Assert.IsTrue(session.TryRebase(fresh));
			Assert.AreSame(first, session.Matches[0].TElement);
			Assert.AreSame(second, session.Matches[1].TElement);
			Assert.AreEqual(0, session.Matches[1].StartIndex);
			Assert.AreEqual(2, session.ReplaceAll());
			Assert.AreEqual("fox |fox", BodyText(fresh));
			Assert.AreEqual("cat cat", BodyText(page));
		}


		[TestMethod]
		public void TryRebase_TargetCrossesSplit_RejectsWithoutPartiallyRemapping()
		{
			var page = CreatePage("cat cat");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "fox", false, true));
			var original = session.Current.TElement;
			var fresh = SearchAndReplacePageState.CreateSaveCopy(page);
			fresh.Root.Elements(Ns + "Outline").Descendants(Ns + "T").Single()
				.ReplaceWith(new XElement(Ns + "T", new XCData("cat c")),
					new XElement(Ns + "T", new XCData("at")));

			Assert.IsFalse(session.TryRebase(fresh));
			Assert.AreSame(original, session.Matches[0].TElement);
			Assert.AreSame(original, session.Matches[1].TElement);
			Assert.AreEqual(0, session.Matches[0].StartIndex);
			Assert.AreEqual(4, session.Matches[1].StartIndex);
			Assert.AreEqual(0, session.CurrentIndex);
			Assert.AreEqual(0, session.ReplacedCount);
			Assert.AreEqual("cat cat", BodyText(page));
			session.Rescan();
			Assert.AreEqual(2, session.ReplaceAll());
			Assert.AreEqual("fox fox", BodyText(page));
			Assert.AreEqual("cat c|at", BodyText(fresh));
		}


		[TestMethod]
		public void TryRebase_MergedSelectedRun_RescanKeepsOriginalScope()
		{
			var paragraph = Paragraph("body", "cat ");
			paragraph.Add(new XElement(Ns + "T", new XAttribute("selected", "all"),
				new XCData("cat")), new XElement(Ns + "T", new XCData(" cat")));
			var page = new Page(new PageBuilder().WithElement(paragraph).BuildElement());
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "catcat", false, true));
			Assert.AreEqual(1, session.RemainingCount);
			var fresh = SearchAndReplacePageState.CreateSaveCopy(page);
			var freshParagraph = fresh.Root.Descendants(Ns + "OE")
				.Single(oe => (string)oe.Attribute("objectID") == "body");
			freshParagraph.Elements(Ns + "T").Remove();
			freshParagraph.Add(new XElement(Ns + "T", new XAttribute("selected", "all"),
				new XCData("cat cat cat")));

			Assert.IsTrue(session.TryRebase(fresh));
			Assert.AreEqual(4, session.Current.StartIndex);
			Assert.AreEqual(1, session.ReplaceAll());
			session.Rescan();
			Assert.AreEqual(2, session.RemainingCount);
			Assert.AreEqual(2, session.ReplaceAll());
			Assert.AreEqual("cat catcatcatcat cat", BodyText(fresh));
		}


		[DataTestMethod]
		[DataRow(true)]
		[DataRow(false)]
		public void Checkpoint_RebaseAndReplacement_RestoresPageRunsAndScope(bool checkpointBeforeRebase)
		{
			var page = CreatePage("cat cat cat");
			var session = new SearchAndReplaceSession(page,
				new SearchAndReplaceEditor("cat", "kitten", false, true));
			Assert.IsTrue(session.ReplaceCurrent());
			var current = session.Current;
			var originalRun = current.TElement;
			var checkpoint = checkpointBeforeRebase ? session.CreateCheckpoint() : null;
			var fresh = SearchAndReplacePageState.CreateSaveCopy(page);
			var first = new XElement(Ns + "T", new XCData("kitten "));
			var second = new XElement(Ns + "T", new XCData("cat cat"));
			fresh.Root.Elements(Ns + "Outline").Descendants(Ns + "T").Single()
				.ReplaceWith(first, second);
			Assert.IsTrue(session.TryRebase(fresh));
			if (!checkpointBeforeRebase) checkpoint = session.CreateCheckpoint();
			Assert.AreEqual(2, session.ReplaceAll());

			session.RestoreCheckpoint(checkpoint);

			Assert.AreSame(current, session.Current);
			Assert.AreSame(checkpointBeforeRebase ? originalRun : second, current.TElement);
			Assert.AreEqual(checkpointBeforeRebase ? 7 : 0, current.StartIndex);
			Assert.AreEqual(checkpointBeforeRebase ? 11 : 4, session.Matches[2].StartIndex);
			Assert.AreEqual(1, session.CurrentIndex);
			Assert.AreEqual(1, session.ReplacedCount);
			Assert.AreEqual(2, session.RemainingCount);
			Assert.IsTrue(session.Matches[0].IsReplaced);
			Assert.AreEqual("kitten cat cat", BodyText(page));
			if (!checkpointBeforeRebase) Assert.AreEqual("kitten |cat cat", BodyText(fresh));
			session.Rescan();
			Assert.AreEqual(2, session.ReplaceAll());
			Assert.AreEqual(checkpointBeforeRebase ? "kitten kitten kitten" : "kitten |kitten kitten",
				BodyText(checkpointBeforeRebase ? page : fresh));
		}


		private static Page CreatePage(string text) =>
			new Page(new PageBuilder().WithParagraph(text, selected: true).BuildElement());

		private static XElement Paragraph(string id, string text) =>
			new XElement(Ns + "OE", new XAttribute("objectID", id),
				new XElement(Ns + "T", new XCData(text)));

		private static string BodyText(Page page) =>
			string.Join("|", page.Root.Elements(Ns + "Outline").Descendants(Ns + "T")
				.Select(t => t.GetCData().GetWrapper().Value));
	}
}
