//************************************************************************************************
// Copyright © 2025 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Commands
{
	using System.Xml.Linq;


	internal class MatchInfo
	{
		public XElement TElement { get; set; }
		public int StartIndex { get; set; }
		public int Length { get; set; }
		public string MatchedText { get; set; }
		public string ParagraphObjectId { get; set; }

		// Replacement values are expanded at scan time, so later edits cannot change
		// capture groups or cause replacement-generated matches to enter the session.
		internal string ReplacementText { get; set; }
		internal XElement ReplacementElement { get; set; }
		internal bool IsReplaced { get; set; }
		internal int ReplacementLength => ReplacementElement?.Value.Length
			?? ReplacementText?.Length ?? 0;
	}
}
