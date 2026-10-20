//************************************************************************************************
// Copyright © 2025 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Commands
{
	using River.OneMoreAddIn.Models;
	using River.OneMoreAddIn.UI;
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows.Forms;
	using System.Xml.Linq;
	using Resx = Properties.Resources;


	internal partial class SearchAndReplaceSessionDialog : MoreForm
	{
		private Page page;
		private readonly SearchAndReplaceEditor editor;
		private readonly string pageId;
		private readonly bool replaceAllOnOpen;
		private SearchAndReplaceSession session;
		private DateTime expectedLastModified;
		private bool busy;
		private bool needsRefresh;
		private bool previewReady;
		private int replacedCount;


		public SearchAndReplaceSessionDialog(Page page, List<MatchInfo> matches,
			SearchAndReplaceEditor editor, string pageId, bool replaceAllOnOpen = false)
		{
			InitializeComponent();
			Text = Resx.SearchAndReplaceSessionDialog_Text;
			replaceButton.Text = Resx.word_Replace;
			closeButton.Text = Resx.word_Close;
			replaceAllButton.Text = Resx.SearchAndReplaceSessionDialog_replaceAllButton_Text;
			previousButton.Text = Resx.SearchAndReplaceSessionDialog_previous;
			nextButton.Text = Resx.SearchAndReplaceSessionDialog_next;
			refreshButton.Text = Resx.SearchAndReplaceSessionDialog_research;
			wrapBox.Text = Resx.SearchAndReplaceSessionDialog_wrap;
			previewLabel.Text = Resx.SearchAndReplaceSessionDialog_preview;
			replacementLabel.Text = Resx.SearchAndReplaceDialog_withLabel_Text;
			helpLabel.Text = Resx.SearchAndReplaceSessionDialog_help;

			this.page = page;
			this.editor = editor;
			this.pageId = pageId;
			this.replaceAllOnOpen = replaceAllOnOpen;
			session = new SearchAndReplaceSession(page, editor, matches);
			expectedLastModified = SearchAndReplacePageState.GetLastModifiedTime(page);
			UpdateStatus();
			Shown += ShowFirstMatch;
		}


		private async void ShowFirstMatch(object sender, EventArgs e)
		{
			await PresentCurrent();
			if (replaceAllOnOpen && !IsDisposed)
			{
				await ReplaceMatches(true);
			}
		}


		private void UpdateStatus(string message = null)
		{
			statusLabel.Text = message ?? (session.RemainingCount == 0
				? string.Format(Resx.SearchAndReplaceSessionDialog_complete, replacedCount)
				: string.Format(Resx.SearchAndReplaceSessionDialog_progress,
					session.Current is null ? 0 : session.CurrentIndex + 1,
					session.Matches.Count, session.RemainingCount, replacedCount));
			pageLabel.Text = string.Format(Resx.SearchAndReplaceSessionDialog_page, page.Title);
			var ready = !busy && !needsRefresh;
			replaceButton.Enabled = ready && previewReady && session.Current is not null;
			replaceAllButton.Enabled = ready && session.RemainingCount > 0;
			previousButton.Enabled = nextButton.Enabled = ready && session.RemainingCount > 0;
			refreshButton.Enabled = !busy;
			wrapBox.Enabled = !busy;
			closeButton.Enabled = !busy;
		}


		private void ShowPreview()
		{
			previewBox.Clear();
			replacementBox.Clear();
			var current = session.Current;
			if (current is null || needsRefresh) return;

			// OneNote navigates to paragraphs, not character offsets. Highlight the exact
			// replacement target here without modifying the user's page formatting.
			var runs = current.TElement.Parent.Elements(page.Namespace + "T").ToList();
			var preceding = string.Concat(runs.TakeWhile(t => t != current.TElement)
				.Select(t => t.GetCData().GetWrapper().Value));
			var text = string.Concat(runs.Select(t => t.GetCData().GetWrapper().Value));
			var start = preceding.Length + current.StartIndex;
			var prefix = NormalizeNewlines(text.Substring(0, start));
			var selected = NormalizeNewlines(text.Substring(start, current.Length));
			text = NormalizeNewlines(text);
			if (current.Length == 0)
			{
				text = text.Insert(prefix.Length, "│");
				selected = "│";
			}

			previewBox.Text = text;
			previewBox.Select(prefix.Length, selected.Length);
			previewBox.SelectionBackColor = Color.Gold;
			previewBox.SelectionColor = Color.Black;
			previewBox.ScrollToCaret();
			replacementBox.Text = current.ReplacementElement is not null
				? current.ReplacementElement.ToString()
				: current.ReplacementText;
		}


		private static string NormalizeNewlines(string text) =>
			text.Replace("\r\n", "\n").Replace('\r', '\n');


		private async Task PresentCurrent(string message = null)
		{
			busy = true;
			previewReady = false;
			UpdateStatus(message);
			try
			{
				ShowPreview();
				previewReady = session.Current is not null && !needsRefresh;
				if (previewReady)
				{
					try
					{
						using (var one = new OneNote())
						{
							if (!await one.NavigateTo(pageId, session.Current.ParagraphObjectId))
							{
								message = Resx.SearchAndReplaceSessionDialog_navigationFailed;
							}
						}
					}
					catch (Exception exc)
					{
						logger.WriteLine("error navigating to match", exc);
						message = Resx.SearchAndReplaceSessionDialog_navigationFailed;
					}
				}
			}
			catch (Exception exc)
			{
				logger.WriteLine("error previewing match", exc);
				needsRefresh = true;
				message = Resx.SearchAndReplaceSessionDialog_previewFailed;
			}
			finally
			{
				busy = false;
				if (!IsDisposed) UpdateStatus(message);
			}
		}


		private async void ReplaceOne(object sender, EventArgs e) => await ReplaceMatches(false);

		private async void ReplaceAll(object sender, EventArgs e) => await ReplaceMatches(true);


		private async Task ReplaceMatches(bool all)
		{
			if (busy || needsRefresh ||
				(all ? session.RemainingCount == 0 : session.Current is null || !previewReady))
			{
				return;
			}

			busy = true;
			UpdateStatus();
			var checkpoint = session.CreateCheckpoint();
			var saved = false;
			string message = null;
			try
			{
				using (var one = new OneNote())
				{
					var livePage = await one.GetPage(pageId, OneNote.PageDetail.Basic);
					if (SearchAndReplacePageState.HasChanged(expectedLastModified, livePage) ||
						!SearchAndReplacePageState.HasSameText(page, livePage))
					{
						needsRefresh = true;
						message = Resx.SearchAndReplaceSessionDialog_pageChanged;
					}
					else
					{
						var count = all ? session.ReplaceAll()
							: session.ReplaceCurrent(wrapBox.Checked) ? 1 : 0;
						// Update optimizes its Page destructively. Never pass the session page.
						saved = await one.Update(SearchAndReplacePageState.CreateSaveCopy(page),
							expectedLastModified: expectedLastModified);
						if (saved)
						{
							replacedCount += count;
							livePage = await one.GetPage(pageId, OneNote.PageDetail.Basic);
							if (!session.TryRebase(livePage))
							{
								needsRefresh = true;
								message = Resx.SearchAndReplaceSessionDialog_pageChanged;
							}
							else
							{
								page = livePage;
								expectedLastModified = SearchAndReplacePageState.GetLastModifiedTime(livePage);
							}
						}
						else
						{
							needsRefresh = true;
							message = Resx.SearchAndReplaceSessionDialog_saveFailed;
						}
					}
				}
			}
			catch (Exception exc)
			{
				logger.WriteLine("error replacing matches", exc);
				needsRefresh = true;
				message = Resx.SearchAndReplaceSessionDialog_saveFailed;
			}
			finally
			{
				if (!saved) session.RestoreCheckpoint(checkpoint);
				busy = false;
			}

			await PresentCurrent(message);
		}


		private async void NextMatch(object sender, EventArgs e)
		{
			if (busy || needsRefresh) return;
			var moved = session.MoveNext(wrapBox.Checked);
			await PresentCurrent(moved ? null : Resx.SearchAndReplaceSessionDialog_boundary);
		}


		private async void PreviousMatch(object sender, EventArgs e)
		{
			if (busy || needsRefresh) return;
			var moved = session.MovePrevious(wrapBox.Checked);
			await PresentCurrent(moved ? null : Resx.SearchAndReplaceSessionDialog_boundary);
		}


		private async void RefreshMatches(object sender, EventArgs e)
		{
			if (busy) return;
			busy = true;
			UpdateStatus();
			string message = null;
			try
			{
				using (var one = new OneNote())
				{
					var fresh = await one.GetPage(pageId, OneNote.PageDetail.Basic);
					// This explicitly labelled action scans the entire original page,
					// independent of the caret/selection left by paragraph navigation.
					fresh.Root.DescendantsAndSelf().Attributes("selected").Remove();
					var freshSession = new SearchAndReplaceSession(fresh, editor);
					page = fresh;
					session = freshSession;
					expectedLastModified = SearchAndReplacePageState.GetLastModifiedTime(page);
					needsRefresh = false;
				}
			}
			catch (Exception exc)
			{
				logger.WriteLine("error refreshing search", exc);
				needsRefresh = true;
				message = Resx.SearchAndReplaceSessionDialog_refreshFailed;
			}
			finally
			{
				busy = false;
			}
			await PresentCurrent(message);
		}


		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.F3 || keyData == (Keys.Shift | Keys.F3))
			{
				if (keyData == Keys.F3) NextMatch(this, EventArgs.Empty);
				else PreviousMatch(this, EventArgs.Empty);
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}


		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (busy && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
			base.OnFormClosing(e);
		}


		private void CloseSession(object sender, EventArgs e) => Close();
	}
}
