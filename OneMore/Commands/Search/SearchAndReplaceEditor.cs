//************************************************************************************************
// Copyright © 2020 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Commands
{
	using River.OneMoreAddIn.Models;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;


	/// <summary>
	/// Searches OneNote text runs, preserving formatting when replacing text or XML.
	/// </summary>
	internal class SearchAndReplaceEditor
	{
		private readonly Regex regex;
		private readonly string replacementString;
		private readonly XElement replacementElement;


		public SearchAndReplaceEditor(
			string pattern, string replacement, bool enableRegex, bool caseSensitive)
		{
			regex = new Regex(
				enableRegex ? pattern : Regex.Escape(pattern),
				caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

			replacementString = replacement ?? string.Empty;
			CaseSensitive = caseSensitive;
			EnableRegex = enableRegex;
			SearchPattern = pattern;
		}


		public SearchAndReplaceEditor(
			string pattern, XElement element, bool enableRegex, bool caseSensitive)
			: this(pattern, string.Empty, enableRegex, caseSensitive)
		{
			replacementElement = element;
		}


		public bool CaseSensitive { get; private set; }
		public bool EnableRegex { get; private set; }
		public string ReplacementString => replacementString;
		public string SearchPattern { get; private set; }


		public int SearchAndReplace(Page page)
		{
			var count = GetSearchRuns(page).Sum(ScanElement);
			if (count > 0)
			{
				PatchEndingBreaks(page);
			}

			return count;
		}


		public int SearchAndReplace(XElement paragraph)
		{
			var ns = paragraph.GetNamespaceOfPrefix(OneNote.Prefix);
			// Includes nested paragraphs and tables in document order.
			return paragraph.Descendants(ns + "T").Sum(ScanElement);
		}


		/// <summary>
		/// Captures the initial selection once; later navigation must not narrow the scope.
		/// </summary>
		internal List<XElement> GetSearchRuns(Page page)
		{
			return new SelectionRange(page)
				.GetSelections(defaulToAnytIfNoRange: true).ToList();
		}


		public string FindFirstMatchObjectId(XElement scope)
		{
			var ns = scope.GetNamespaceOfPrefix(OneNote.Prefix);
			foreach (var element in scope.Descendants(ns + "T"))
			{
				var matches = new List<MatchInfo>();
				CollectMatches(element, matches);
				if (matches.Count > 0)
				{
					return matches[0].ParagraphObjectId;
				}
			}

			return null;
		}


		private int ScanElement(XElement element)
		{
			var matches = new List<MatchInfo>();
			CollectMatches(element, matches);
			if (matches.Count == 0)
			{
				return 0;
			}

			var cdata = element.GetCData();
			var wrapper = cdata.GetWrapper();
			// Snapshot matches, then work backwards so offsets and capture values stay valid.
			for (var i = matches.Count - 1; i >= 0; i--)
			{
				ApplyMatch(wrapper, matches[i]);
			}

			cdata.Value = wrapper.GetInnerXml();
			return matches.Count;
		}


		public List<MatchInfo> FindAllMatches(Page page)
		{
			return FindAllMatches(GetSearchRuns(page));
		}


		internal List<MatchInfo> FindAllMatches(IEnumerable<XElement> runs)
		{
			var matches = new List<MatchInfo>();
			foreach (var run in runs)
			{
				CollectMatches(run, matches);
			}

			return matches;
		}


		private void CollectMatches(XElement element, List<MatchInfo> results)
		{
			if (element.IsMathML())
			{
				return;
			}

			var wrapper = element.GetCData().GetWrapper();
			var rawtext = wrapper.Value.Replace('\u00A0', ' ');
			var objectId = element.Ancestors()
				.FirstOrDefault(e => e.Name.LocalName == "OE")?.Attribute("objectID")?.Value
				?? string.Empty;

			var insertsLink = replacementElement is not null &&
				replacementElement.DescendantsAndSelf().Any(e => e.Name.LocalName == "a");
			var previousEnd = -1;

			foreach (Match match in regex.Matches(rawtext))
			{
				// OneMore's existing capture-group mode replaces the first capture, using
				// the rest as context. Keep that contract for LinkReferences and hashtags.
				var target = match.Groups.Count > 1 ? match.Groups[1] : match.Groups[0];
				if (!target.Success || target.Index < previousEnd ||
					(insertsLink && OverlapsExistingLink(wrapper, target.Index, target.Length)))
				{
					continue;
				}

				var result = new MatchInfo
				{
					TElement = element,
					StartIndex = target.Index,
					Length = target.Length,
					MatchedText = target.Value,
					ParagraphObjectId = objectId
				};

				if (replacementElement is not null)
				{
					result.ReplacementElement = ExpandElement(match);
				}
				else
				{
					result.ReplacementText = NormalizeNewlines(EnableRegex
						? match.Result(replacementString) : replacementString);
				}

				results.Add(result);
				previousEnd = target.Index + target.Length;
			}
		}


		// Expand XML node values individually so captured &, <, or quotes remain text.
		private XElement ExpandElement(Match match)
		{
			var element = new XElement(replacementElement);
			if (EnableRegex)
			{
				foreach (var attribute in element.DescendantsAndSelf().Attributes())
				{
					attribute.Value = match.Result(attribute.Value);
				}
			}

			foreach (var text in element.DescendantNodes().OfType<XText>())
			{
				text.Value = NormalizeNewlines(EnableRegex ? match.Result(text.Value) : text.Value);
			}

			return element;
		}


		// XML reparsing normalizes literal CRLF/CR. Normalize the snapshot as well so
		// its replacement length still describes the text after the next GetWrapper.
		private static string NormalizeNewlines(string text) =>
			text.Replace("\r\n", "\n").Replace('\r', '\n');


		private static bool OverlapsExistingLink(XElement wrapper, int index, int length)
		{
			var texts = wrapper.DescendantNodes().OfType<XText>().ToList();
			var offset = 0;
			for (var i = 0; i < texts.Count; i++)
			{
				var text = texts[i];
				var end = offset + text.Value.Length;
				// Zero-width matches belong to the same insertion node as ApplyMatch:
				// the node to the right of a boundary, or the final node at run end.
				var overlaps = length == 0
					? index >= offset && (index < end || (index == end && i == texts.Count - 1))
					: index < end && index + length > offset;
				if (overlaps &&
					text.Ancestors().Any(e => e.Name.LocalName == "a"))
				{
					return true;
				}

				offset = end;
			}

			return false;
		}


		/// <summary>
		/// Applies an already-expanded snapshot match. Returns false for stale text instead
		/// of silently replacing a different occurrence at the same offset.
		/// </summary>
		public bool ReplaceMatch(MatchInfo match)
		{
			if (match.TElement.IsMathML())
			{
				return false;
			}

			var cdata = match.TElement.GetCData();
			var wrapper = cdata.GetWrapper();
			var rawtext = wrapper.Value.Replace('\u00A0', ' ');
			if (match.StartIndex < 0 || match.StartIndex > rawtext.Length - match.Length ||
				rawtext.Substring(match.StartIndex, match.Length) != match.MatchedText)
			{
				return false;
			}

			ApplyMatch(wrapper, match);
			cdata.Value = wrapper.GetInnerXml();
			return true;
		}


		// Walk actual descendant text nodes rather than flattening SPANs or anchors. Their
		// original offsets remain fixed for this operation, preserving unrelated formatting.
		private static void ApplyMatch(XElement wrapper, MatchInfo match)
		{
			var texts = wrapper.DescendantNodes().OfType<XText>().ToList();
			var offset = 0;
			var end = match.StartIndex + match.Length;
			var inserted = false;

			for (var i = 0; i < texts.Count; i++)
			{
				var text = texts[i];
				var value = text.Value;
				var nodeEnd = offset + value.Length;
				if (!inserted && match.StartIndex >= offset &&
					(match.StartIndex < nodeEnd || (match.StartIndex == nodeEnd && i == texts.Count - 1)))
				{
					var local = match.StartIndex - offset;
					var length = Math.Min(match.Length, value.Length - local);
					if (match.ReplacementElement is not null)
					{
						text.ReplaceWith(value.Substring(0, local),
							new XElement(match.ReplacementElement), value.Substring(local + length));
					}
					else
					{
						text.Value = value.Remove(local, length)
							.Insert(local, match.ReplacementText ?? string.Empty);
					}

					inserted = true;
				}
				else if (inserted && offset < end)
				{
					text.Value = value.Remove(0, Math.Min(value.Length, end - offset));
				}

				offset = nodeEnd;
				if (inserted && offset >= end)
				{
					break;
				}
			}

			// Empty runs and zero-width end-of-run matches are valid insertion points.
			if (!inserted)
			{
				if (match.ReplacementElement is not null)
				{
					wrapper.Add(new XElement(match.ReplacementElement));
				}
				else
				{
					wrapper.Add(new XText(match.ReplacementText ?? string.Empty));
				}
			}
		}


		internal void PatchEndingBreaks(Page page)
		{
			foreach (var run in page.Root.Elements(page.Namespace + "Outline")
				.Descendants(page.Namespace + "T"))
			{
				var cdata = run.GetCData();
				if (cdata.Value.EndsWith("<br>\n"))
				{
					cdata.Value += "&nbsp;";
				}
			}
		}
	}
}
