# Search and replace session

Use a disposable OneNote page with a title, two separate textboxes (outlines), a
table, and a paragraph with bold/italic text. Put several occurrences of `cat`
in each textbox, including adjacent occurrences and multiple occurrences in
the same paragraph.

## Interactive replacement

1. Open Search and Replace, find `cat`, replace with `dog`, choose This page,
   and click Replace Next.
2. Before clicking Replace in the session, confirm the paragraph is visible in
   OneNote and the exact occurrence is highlighted in the dialog preview. The
   replacement preview must show `dog`.
3. Replace one occurrence at a time through both textboxes and the table.
   Counts should remain consistent and the window must remain open after the
   last replacement. Close it with Close, Escape, or the window's close button.
4. Repeat with an empty replacement (deletion), a shorter replacement, and a
   longer replacement. No shifted occurrence should be skipped.
5. Use Next / Previous (or F3 / Shift+F3). With Wrap around enabled, navigation
   cycles through remaining matches. With it disabled, navigation stops at the
   boundary and reports that fact. Next does not remove a match from Replace All.
6. Replace one occurrence, navigate past another, then Replace All. All remaining
   matches in the original search must be replaced once, across all textboxes.
7. Find `cat` and replace with `catcat`, then Replace All. The operation must
   terminate. Newly inserted matches are included only after Re-search page.
8. Click Re-search page after completion. It must reload and scan the whole
   original page, even if the caret is now in one textbox or another page is open.
9. Start with a selected range. Initial replacement must respect that range.
   Re-search page deliberately starts a new search across the entire page.
10. Start with no matches. The session must remain open with re-search available.
11. Choose Replace All directly in the initial page-scope dialog. Replacement
    should finish with the session still open and the completed count visible.

## Preview and data preservation

- Test case-sensitive and insensitive text, Unicode, nonbreaking spaces, line
  breaks, and matches spanning differently formatted spans.
- Test regular expressions and replacement substitutions, including zero-width
  matches. A zero-width target is shown with a vertical marker in the preview.
- OneMore retains its existing regex convention: when there are capturing groups,
  the first capture is replaced. The highlighted preview must show that capture,
  and the replacement preview must show the expanded replacement.
- Preview highlighting must remain visible when a button has focus, including
  in dark mode. It must not change highlighting or formatting on the actual page.
- Resize the dialog and check high DPI and long page titles/status messages.
- Edit the page while the session is open, then try Replace. It must request a
  re-search rather than overwrite the edit. Repeat with a formatting change.
- If a save fails, no successful replacement should be counted and a re-search
  must be required before another write.
- Reopen the command while its session is open: the existing session should be
  brought forward rather than creating a second session over the same snapshot.

The exact character highlight is in the dialog preview. OneNote's automation API
navigates to the containing paragraph; it does not expose exact character selection.
Live OneNote smoke testing is still required for native navigation, save-time XML
normalization, DPI/theme behavior, and simultaneous coauthor edits.
