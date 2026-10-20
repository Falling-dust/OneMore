//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Commands
{
	using River.OneMoreAddIn.Models;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Xml.Linq;


	/// <summary>
	/// Navigates one search snapshot. Replacements never add new matches until Rescan.
	/// </summary>
	internal sealed class SearchAndReplaceSession
	{
		private Page page;
		private readonly SearchAndReplaceEditor editor;
		private List<(XElement Run, int Start, int Length)> scopes;
		private List<MatchInfo> matches;


		public SearchAndReplaceSession(Page page, SearchAndReplaceEditor editor,
			List<MatchInfo> matches = null)
		{
			this.page = page ?? throw new ArgumentNullException(nameof(page));
			this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
			var runs = editor.GetSearchRuns(page);
			scopes = runs.Select(run => (run, 0, run.GetCData().GetWrapper().Value.Length)).ToList();
			this.matches = matches is null
				? editor.FindAllMatches(runs) : new List<MatchInfo>(matches);
			CurrentIndex = this.matches.Count > 0 ? 0 : -1;
		}


		public IReadOnlyList<MatchInfo> Matches => matches;
		public int CurrentIndex { get; private set; }
		public MatchInfo Current => CurrentIndex >= 0 && CurrentIndex < matches.Count &&
			!matches[CurrentIndex].IsReplaced ? matches[CurrentIndex] : null;
		public int ReplacedCount { get; private set; }
		public int RemainingCount => matches.Count - ReplacedCount;


		public bool MoveNext(bool wrap) => Move(1, wrap);
		public bool MovePrevious(bool wrap) => Move(-1, wrap);


		private bool Move(int direction, bool wrap)
		{
			if (RemainingCount == 0) return false;

			var index = CurrentIndex;
			for (var step = 0; step < matches.Count; step++)
			{
				index += direction;
				if (index < 0 || index >= matches.Count)
				{
					if (!wrap) return false;
					index = direction > 0 ? 0 : matches.Count - 1;
				}

				if (!matches[index].IsReplaced)
				{
					CurrentIndex = index;
					return true;
				}
			}

			return false;
		}


		public bool ReplaceCurrent(bool wrap = true)
		{
			if (Current is null) return false;
			ReplaceAt(CurrentIndex);
			editor.PatchEndingBreaks(page);
			MoveNext(wrap);
			return true;
		}


		/// <summary>
		/// Replaces every pending match, including matches skipped with Next/Previous.
		/// Applying the snapshot backwards also handles replacements containing the query.
		/// </summary>
		public int ReplaceAll()
		{
			var count = 0;
			for (var i = matches.Count - 1; i >= 0; i--)
			{
				if (!matches[i].IsReplaced)
				{
					ReplaceAt(i);
					count++;
				}
			}

			if (count > 0) editor.PatchEndingBreaks(page);
			return count;
		}


		private void ReplaceAt(int index)
		{
			var match = matches[index];
			if (!match.TElement.Ancestors().Contains(page.Root) || !editor.ReplaceMatch(match))
			{
				// The dialog restores its checkpoint and requests a fresh search. Never
				// skip stale text silently or replace a different occurrence instead.
				throw new InvalidOperationException("The search result has changed. Search again.");
			}

			match.IsReplaced = true;
			ReplacedCount++;
			var delta = match.ReplacementLength - match.Length;
			for (var i = 0; i < scopes.Count; i++)
			{
				var scope = scopes[i];
				if (scope.Run != match.TElement) continue;
				if (match.StartIndex < scope.Start)
				{
					scopes[i] = (scope.Run, scope.Start + delta, scope.Length);
				}
				else if (match.StartIndex + match.Length <= scope.Start + scope.Length)
				{
					scopes[i] = (scope.Run, scope.Start, scope.Length + delta);
				}
			}

			for (var i = index + 1; i < matches.Count; i++)
			{
				var pending = matches[i];
				if (!pending.IsReplaced && pending.TElement == match.TElement)
				{
					pending.StartIndex += delta;
				}
			}
		}


		/// <summary>
		/// Re-searches the original runs, independent of the selection left by navigation.
		/// </summary>
		public void Rescan()
		{
			matches = editor.FindAllMatches(scopes.Select(scope => scope.Run).Distinct())
				.Where(match => scopes.Any(scope => scope.Run == match.TElement &&
					match.StartIndex >= scope.Start &&
					match.StartIndex + match.Length <= scope.Start + scope.Length)).ToList();
			ReplacedCount = 0;
			CurrentIndex = matches.Count > 0 ? 0 : -1;
		}


		/// <summary>
		/// Adopts the saved page's latest formatting and nontext content without rescanning
		/// the query. OneNote may merge or split T runs, so remap pending targets using
		/// paragraph offsets. Unsupported splits fail atomically and require a fresh search.
		/// </summary>
		public bool TryRebase(Page fresh)
		{
			if (!SearchAndReplacePageState.HasSameText(page, fresh)) return false;

			var oldParagraphs = page.Root.Descendants(page.Namespace + "OE").ToList();
			var newParagraphs = fresh.Root.Descendants(fresh.Namespace + "OE").ToList();
			var paragraphMap = oldParagraphs.Select((paragraph, index) =>
				(paragraph, replacement: newParagraphs[index]))
				.ToDictionary(pair => pair.paragraph, pair => pair.replacement);
			var updates = new List<(MatchInfo Match, XElement Run, int Start)>();
			var newScopes = new List<(XElement Run, int Start, int Length)>();

			foreach (var match in matches.Where(match => !match.IsReplaced))
			{
				if (match.TElement.Parent is null ||
					!paragraphMap.TryGetValue(match.TElement.Parent, out var paragraph)) return false;
				var absolute = GetRunOffset(match.TElement) + match.StartIndex;
				var target = FindRun(paragraph, absolute, match.Length);
				if (target.Run is null || target.Run.IsMathML()) return false;
				var text = target.Run.GetCData().GetWrapper().Value.Replace('\u00A0', ' ');
				if (text.Substring(target.Start, match.Length) != match.MatchedText) return false;
				updates.Add((match, target.Run, target.Start));
			}

			foreach (var scope in scopes)
			{
				if (scope.Run.Parent is null ||
					!paragraphMap.TryGetValue(scope.Run.Parent, out var paragraph)) return false;
				var start = GetRunOffset(scope.Run) + scope.Start;
				if (scope.Length == 0)
				{
					var target = FindRun(paragraph, start, 0);
					if (target.Run is null) return false;
					newScopes.Add((target.Run, target.Start, 0));
					continue;
				}

				var offset = 0;
				foreach (var run in paragraph.Elements(fresh.Namespace + "T"))
				{
					var length = run.GetCData().GetWrapper().Value.Length;
					var overlapStart = Math.Max(start, offset);
					var overlapEnd = Math.Min(start + scope.Length, offset + length);
					if (overlapEnd > overlapStart)
					{
						newScopes.Add((run, overlapStart - offset, overlapEnd - overlapStart));
					}
					offset += length;
				}
			}

			// Commit only after every target has a valid location. Replaced entries retain
			// their flags and expanded values; they must never become new pending matches.
			foreach (var update in updates)
			{
				update.Match.TElement = update.Run;
				update.Match.StartIndex = update.Start;
			}
			page = fresh;
			scopes = MergeScopes(newScopes);
			return true;
		}


		private static int GetRunOffset(XElement run) =>
			run.Parent.Elements(run.Name).TakeWhile(sibling => sibling != run)
				.Sum(sibling => sibling.GetCData().GetWrapper().Value.Length);


		private static (XElement Run, int Start) FindRun(XElement paragraph, int start, int length)
		{
			var runs = paragraph.Elements(paragraph.Name.Namespace + "T").ToList();
			var offset = 0;
			for (var i = 0; i < runs.Count; i++)
			{
				var end = offset + runs[i].GetCData().GetWrapper().Value.Length;
				if (start >= offset && start + length <= end &&
					(start < end || (length == 0 && i == runs.Count - 1)))
				{
					return (runs[i], start - offset);
				}
				offset = end;
			}
			return (null, 0);
		}


		private static List<(XElement Run, int Start, int Length)> MergeScopes(
			List<(XElement Run, int Start, int Length)> mapped)
		{
			var merged = new List<(XElement Run, int Start, int Length)>();
			foreach (var group in mapped.GroupBy(scope => scope.Run))
			{
				foreach (var scope in group.OrderBy(scope => scope.Start))
				{
					if (merged.Count > 0)
					{
						var previous = merged[merged.Count - 1];
						if (previous.Run == scope.Run && scope.Start <= previous.Start + previous.Length)
						{
							merged[merged.Count - 1] = (scope.Run, previous.Start,
								Math.Max(previous.Start + previous.Length, scope.Start + scope.Length) - previous.Start);
							continue;
						}
					}
					merged.Add(scope);
				}
			}
			return merged;
		}


		public Checkpoint CreateCheckpoint() => new Checkpoint(this);


		public void RestoreCheckpoint(Checkpoint checkpoint)
		{
			if (checkpoint is null || checkpoint.Owner != this)
			{
				throw new ArgumentException("Checkpoint belongs to another search session.", nameof(checkpoint));
			}

			// Retain the XElement instances referenced by matches and original scope.
			page = checkpoint.Page;
			scopes = checkpoint.Scopes.ToList();
			foreach (var run in checkpoint.Runs) run.Element.GetCData().Value = run.Value;
			matches = checkpoint.Matches.Select(m => m.Match).ToList();
			foreach (var state in checkpoint.Matches)
			{
				state.Match.TElement = state.Run;
				state.Match.StartIndex = state.StartIndex;
				state.Match.IsReplaced = state.IsReplaced;
			}
			CurrentIndex = checkpoint.CurrentIndex;
			ReplacedCount = checkpoint.ReplacedCount;
		}


		internal sealed class Checkpoint
		{
			internal SearchAndReplaceSession Owner { get; }
			internal Page Page { get; }
			internal List<(XElement Run, int Start, int Length)> Scopes { get; }
			internal List<(XElement Element, string Value)> Runs { get; }
			internal List<(MatchInfo Match, XElement Run, int StartIndex, bool IsReplaced)> Matches { get; }
			internal int CurrentIndex { get; }
			internal int ReplacedCount { get; }


			internal Checkpoint(SearchAndReplaceSession session)
			{
				Owner = session;
				Page = session.page;
				Scopes = session.scopes.ToList();
				Runs = session.page.Root.Descendants(session.page.Namespace + "T")
					.Select(t => (t, t.GetCData().Value)).ToList();
				Matches = session.matches
					.Select(m => (m, m.TElement, m.StartIndex, m.IsReplaced)).ToList();
				CurrentIndex = session.CurrentIndex;
				ReplacedCount = session.ReplacedCount;
			}
		}
	}
}
