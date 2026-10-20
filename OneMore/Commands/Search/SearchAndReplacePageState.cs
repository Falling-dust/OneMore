//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Commands
{
	using River.OneMoreAddIn.Models;
	using System;
	using System.Globalization;
	using System.Linq;
	using System.Xml.Linq;


	/// <summary>
	/// Preserves a replacement session's complete page while saving an optimized copy,
	/// and detects changes made outside the modeless session.
	/// </summary>
	internal static class SearchAndReplacePageState
	{
		public static DateTime GetLastModifiedTime(Page page)
		{
			var value = page?.Root?.Attribute("lastModifiedTime")?.Value;
			return DateTime.TryParse(value, CultureInfo.InvariantCulture,
				DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time)
				? time
				: DateTime.MinValue;
		}


		public static bool HasChanged(Page expected, Page actual)
		{
			return expected is null || actual is null || expected.PageId != actual.PageId ||
				HasChanged(GetLastModifiedTime(expected), actual);
		}


		public static bool HasChanged(DateTime expected, Page actual)
		{
			// An absent revision cannot safely authorize a write to a modeless snapshot.
			return actual?.Root is null || expected == DateTime.MinValue ||
				GetLastModifiedTime(actual) != expected;
		}


		public static Page CreateSaveCopy(Page page)
		{
			if (page?.Root is null)
			{
				throw new ArgumentNullException(nameof(page));
			}

			// Keep omHash attributes: OneNote.Update uses those original hashes to discard
			// unchanged outlines from its payload. It must never discard session content.
			return new Page(new XElement(page.Root));
		}


		/// <summary>
		/// Checks the text after saving before accepting a newly read revision. OneNote can
		/// split or merge T runs at the caret, so compare the complete text of each OE.
		/// This is a text check; concurrency protection for the write is the revision.
		/// </summary>
		public static bool HasSameText(Page expected, Page actual)
		{
			if (expected?.Root is null || actual?.Root is null || expected.PageId != actual.PageId)
			{
				return false;
			}

			var expectedParagraphs = expected.Root.Descendants(expected.Namespace + "OE")
				.Select(GetParagraphText);
			var actualParagraphs = actual.Root.Descendants(actual.Namespace + "OE")
				.Select(GetParagraphText);

			return expectedParagraphs.SequenceEqual(actualParagraphs);
		}


		private static (string objectId, string text) GetParagraphText(XElement paragraph)
		{
			var text = string.Concat(paragraph.Elements(paragraph.Name.Namespace + "T")
				.Select(t => t.GetCData().GetWrapper().Value));

			return (paragraph.Attribute("objectID")?.Value, text);
		}
	}
}
