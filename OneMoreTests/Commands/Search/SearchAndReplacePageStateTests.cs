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
	public class SearchAndReplacePageStateTests
	{
		private static readonly XNamespace Ns =
			"http://schemas.microsoft.com/office/onenote/2013/onenote";


		[TestMethod]
		public void CreateSaveCopy_OptimizingChangedOutline_PreservesOtherSessionMatches()
		{
			var page = CreatePage();
			var first = page.Root.Elements(Ns + "Outline").First().Descendants(Ns + "T").First();
			first.GetCData().Value = "dog cat";

			var saved = SearchAndReplacePageState.CreateSaveCopy(page);
			saved.OptimizeForSave(false);

			Assert.AreEqual(1, saved.Root.Elements(Ns + "Outline").Count(),
				"The save payload should contain only the changed textbox.");
			Assert.AreEqual(2, page.Root.Elements(Ns + "Outline").Count(),
				"The session must retain unchanged textboxes after saving.");
			var editor = new SearchAndReplaceEditor("cat", "dog", false, false);
			Assert.AreEqual(3, editor.FindAllMatches(page).Count,
				"Pending matches in both textboxes must remain searchable.");
		}


		[TestMethod]
		public void CreateSaveCopy_ModifyingCopy_DoesNotMutateSession()
		{
			var page = CreatePage();
			var saved = SearchAndReplacePageState.CreateSaveCopy(page);
			saved.Root.Elements(Ns + "Outline").First()
				.Descendants(Ns + "T").First().GetCData().Value = "changed";

			Assert.AreEqual("cat cat", page.Root.Elements(Ns + "Outline").First()
				.Descendants(Ns + "T").First().GetCData().Value);
		}


		[TestMethod]
		public void HasChanged_NewerRevision_RequiresResearch()
		{
			var page = CreatePage();
			var latest = SearchAndReplacePageState.CreateSaveCopy(page);
			latest.Root.SetAttributeValue("lastModifiedTime", "2026-10-16T02:00:01Z");

			Assert.IsTrue(SearchAndReplacePageState.HasChanged(page, latest));
			Assert.IsTrue(SearchAndReplacePageState.HasChanged(
				SearchAndReplacePageState.GetLastModifiedTime(page), latest));
		}


		[TestMethod]
		public void HasChanged_EquivalentTimeZones_AcceptsSameRevision()
		{
			var page = CreatePage();
			var latest = SearchAndReplacePageState.CreateSaveCopy(page);
			latest.Root.SetAttributeValue("lastModifiedTime", "2026-10-16T10:00:00+08:00");

			Assert.IsFalse(SearchAndReplacePageState.HasChanged(page, latest));
			Assert.AreEqual(DateTimeKind.Utc,
				SearchAndReplacePageState.GetLastModifiedTime(latest).Kind);
		}


		[TestMethod]
		public void HasChanged_MissingPageOrRevision_DoesNotAuthorizeSave()
		{
			var page = CreatePage();
			Assert.IsTrue(SearchAndReplacePageState.HasChanged(page, null));
			Assert.IsTrue(SearchAndReplacePageState.HasChanged(DateTime.MinValue, page));

			page.Root.Attribute("lastModifiedTime").Remove();
			Assert.IsTrue(SearchAndReplacePageState.HasChanged(page,
				SearchAndReplacePageState.CreateSaveCopy(page)));
		}


		[TestMethod]
		public void HasSameText_CaretSplitsRunsAndStylesNormalize_PreservesTextIdentity()
		{
			var page = CreatePage();
			var latest = SearchAndReplacePageState.CreateSaveCopy(page);
			var paragraph = latest.Root.Elements(Ns + "Outline").First().Descendants(Ns + "OE").First();
			paragraph.Element(Ns + "T").ReplaceWith(
				new XElement(Ns + "T", new XCData("<span>cat </span>")),
				new XElement(Ns + "T", new XAttribute("selected", "all"), new XCData(string.Empty)),
				new XElement(Ns + "T", new XCData("cat")));
			latest.Root.SetAttributeValue("lastModifiedTime", "2026-10-16T02:00:01Z");

			Assert.IsTrue(SearchAndReplacePageState.HasSameText(page, latest));
		}


		[TestMethod]
		public void HasSameText_ExternalEditAfterSave_DoesNotAcceptNewRevision()
		{
			var page = CreatePage();
			var latest = SearchAndReplacePageState.CreateSaveCopy(page);
			latest.Root.Elements(Ns + "Outline").Last()
				.Descendants(Ns + "T").First().GetCData().Value = "external edit";

			Assert.IsFalse(SearchAndReplacePageState.HasSameText(page, latest));
		}


		[TestMethod]
		public void HasSameText_ParagraphRemovedOrRecreated_DoesNotAcceptNewRevision()
		{
			var page = CreatePage();
			var latest = SearchAndReplacePageState.CreateSaveCopy(page);
			latest.Root.Elements(Ns + "Outline").Last()
				.Descendants(Ns + "OE").First().SetAttributeValue("objectID", "new-paragraph");
			Assert.IsFalse(SearchAndReplacePageState.HasSameText(page, latest));

			latest.Root.Elements(Ns + "Outline").Last().Remove();
			Assert.IsFalse(SearchAndReplacePageState.HasSameText(page, latest));
		}


		private static Page CreatePage()
		{
			var root = new PageBuilder().WithElement(
				new XElement(Ns + "OE", new XAttribute("objectID", "first"),
					new XElement(Ns + "T", new XCData("cat cat")))).BuildElement();
			root.SetAttributeValue("lastModifiedTime", "2026-10-16T02:00:00Z");
			root.Add(new XElement(Ns + "Outline", new XElement(Ns + "OEChildren",
				new XElement(Ns + "OE", new XAttribute("objectID", "second"),
					new XElement(Ns + "T", new XCData("cat cat"))))));

			return new Page(root);
		}
	}
}
