using System.Reflection;
using Avalonia;
using Caly.Core.Models;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Core;

namespace Caly.Tests
{
    public class TextSelectionTests
    {
        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Creates a horizontal PdfWord whose letters are evenly distributed
        /// across [xStart, xStart+width] × [yBottom, yBottom+height].
        /// </summary>
        private static PdfWord MakeWord(
            string value = "A",
            double xStart = 0,
            double width = 10,
            double yBottom = 0,
            double height = 12)
        {
            var letters = new List<PdfLetter>();
            double letterWidth = width / value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                letters.Add(new PdfLetter(
                    value[i].ToString(),
                    new PdfRectangle(
                        xStart + i * letterWidth, yBottom,
                        xStart + (i + 1) * letterWidth, yBottom + height),
                    12f, 0));
            }
            return new PdfWord(letters);
        }

        /// <summary>
        /// Sets <see cref="PdfWord.IndexInPage"/> via reflection
        /// (the property has <c>internal set</c>).
        /// </summary>
        private static void SetIndexInPage(PdfWord word, ushort index)
        {
            var prop = typeof(PdfWord).GetProperty(nameof(PdfWord.IndexInPage))!;
            prop.SetValue(word, index);
        }

        // -----------------------------------------------------------------------
        // Default state
        // -----------------------------------------------------------------------

        [Fact]
        public void NewSelection_HasCorrectDefaults()
        {
            var sel = new TextSelection(5);

            Assert.Equal(-1, sel.AnchorPageIndex);
            Assert.Equal(-1, sel.FocusPageIndex);
            Assert.Equal(-1, sel.AnchorOffset);
            Assert.Equal(-1d, sel.AnchorOffsetDistance);
            Assert.Equal(-1, sel.FocusOffset);
            Assert.Equal(-1d, sel.FocusOffsetDistance);
            Assert.Null(sel.AnchorWord);
            Assert.Null(sel.FocusWord);
            Assert.Null(sel.AnchorPoint);
            Assert.False(sel.HasStarted);
            Assert.False(sel.IsValid);
            Assert.False(sel.IsSelecting);
        }

        // -----------------------------------------------------------------------
        // Start
        // -----------------------------------------------------------------------

        [Fact]
        public void Start_PageNumber0_ThrowsArgumentOutOfRange()
        {
            var sel = new TextSelection(5);
            Assert.Throws<ArgumentOutOfRangeException>(() => sel.Start(0, null));
        }

        [Fact]
        public void Start_NegativePageNumber_ThrowsArgumentOutOfRange()
        {
            var sel = new TextSelection(5);
            Assert.Throws<ArgumentOutOfRangeException>(() => sel.Start(-1, null));
        }

        [Fact]
        public void Start_ValidPage_SetsAnchorPageAndWord()
        {
            var sel = new TextSelection(5);
            var word = MakeWord();

            sel.Start(2, word);

            Assert.Equal(2, sel.AnchorPageIndex);
            Assert.Same(word, sel.AnchorWord);
            Assert.True(sel.HasStarted);
            Assert.False(sel.IsValid); // FocusWord still null
        }

        [Fact]
        public void Start_NullWord_ValidPage_SetsHasStarted()
        {
            var sel = new TextSelection(5);

            sel.Start(3, null);

            Assert.Equal(3, sel.AnchorPageIndex);
            Assert.Null(sel.AnchorWord);
            Assert.False(sel.HasStarted); // HasStarted requires AnchorWord != null
        }

        [Fact]
        public void Start_WithoutLocation_OffsetsAreMinusOne()
        {
            var sel = new TextSelection(5);

            sel.Start(1, MakeWord());

            Assert.Equal(-1, sel.AnchorOffset);
            Assert.Equal(-1d, sel.AnchorOffsetDistance);
            Assert.Null(sel.AnchorPoint);
        }

        [Fact]
        public void Start_WithLocationAndNullWord_ThrowsNullReferenceException()
        {
            var sel = new TextSelection(5);
            Assert.Throws<NullReferenceException>(() => sel.Start(1, null, new Point(5, 5)));
        }

        [Fact]
        public void Start_FiresTextSelectionStartedEvent()
        {
            var sel = new TextSelection(5);
            int firedCount = 0;
            int capturedPage = -99;
            sel.TextSelectionStarted += (_, e) => { firedCount++; capturedPage = e.AnchorPageIndex; };

            sel.Start(3, null);

            Assert.Equal(1, firedCount);
            Assert.Equal(3, capturedPage);
        }

        [Fact]
        public void Start_WithLocation_SetsAnchorOffset()
        {
            var sel = new TextSelection(5);
            // 5-letter word "HELLO" spanning x=[0,50], y=[0,12]
            var word = MakeWord("HELLO", xStart: 0, width: 50);
            // x=25 falls at normalised position 0.5 → letter index 2 (see FindLetterIndexOver)
            sel.Start(1, word, new Point(25, 6));

            Assert.Equal(2, sel.AnchorOffset);
            Assert.Equal(new Point(25, 6), sel.AnchorPoint);
        }

        // -----------------------------------------------------------------------
        // Extend
        // -----------------------------------------------------------------------

        [Fact]
        public void Extend_PageNumber0_ThrowsArgumentOutOfRange()
        {
            var sel = new TextSelection(5);
            Assert.Throws<ArgumentOutOfRangeException>(() => sel.Extend(0, null));
        }

        [Fact]
        public void Extend_ValidPage_SetsFocusPageAndWord()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord("A"));
            var focusWord = MakeWord("B");

            sel.Extend(2, focusWord);

            Assert.Equal(2, sel.FocusPageIndex);
            Assert.Same(focusWord, sel.FocusWord);
            Assert.True(sel.IsValid);
        }

        [Fact]
        public void Extend_WithoutLocation_FocusOffsetIsMinusOne()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord());

            sel.Extend(1, MakeWord("B"));

            Assert.Equal(-1, sel.FocusOffset);
            Assert.Equal(-1d, sel.FocusOffsetDistance);
        }

        [Fact]
        public void Extend_WithLocationAndNullWord_ThrowsNullReferenceException()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord());
            Assert.Throws<NullReferenceException>(() => sel.Extend(1, null, new Point(5, 5)));
        }

        [Fact]
        public void Extend_FiresTextSelectionExtendedEvent()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord());
            int firedCount = 0;
            sel.TextSelectionExtended += (_, _) => firedCount++;

            sel.Extend(1, MakeWord("B"));

            Assert.Equal(1, firedCount);
        }

        [Fact]
        public void Extend_FocusPageChanged_FiresFocusPageChangedEvent()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord());
            sel.Extend(1, MakeWord("B")); // initial focus on page 1

            int firedCount = 0;
            int oldPage = -99, newPage = -99;
            sel.TextSelectionFocusPageChanged += (_, e) =>
            {
                firedCount++;
                oldPage = e.OldFocusPageIndex;
                newPage = e.NewFocusPageIndex;
            };

            sel.Extend(2, MakeWord("C")); // focus moves to page 2

            Assert.Equal(1, firedCount);
            Assert.Equal(1, oldPage);
            Assert.Equal(2, newPage);
        }

        [Fact]
        public void Extend_FocusPageUnchanged_DoesNotFireFocusPageChangedEvent()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord());
            sel.Extend(2, MakeWord("B")); // focus lands on page 2

            int firedCount = 0;
            sel.TextSelectionFocusPageChanged += (_, _) => firedCount++;

            sel.Extend(2, MakeWord("C")); // still page 2

            Assert.Equal(0, firedCount);
        }

        [Fact]
        public void Extend_WithLocation_SetsFocusOffset()
        {
            var sel = new TextSelection(5);
            var word = MakeWord("HELLO", xStart: 0, width: 50);
            sel.Start(1, word, new Point(5, 6)); // letter 0
            // x=45 → normalised 0.9, last letter (index 4)
            sel.Extend(1, word, new Point(45, 6));

            Assert.Equal(4, sel.FocusOffset);
        }

        // -----------------------------------------------------------------------
        // ResetSelection
        // -----------------------------------------------------------------------

        [Fact]
        public void ResetSelection_ClearsAllState()
        {
            var sel = new TextSelection(5);
            sel.Start(2, MakeWord());
            sel.Extend(3, MakeWord("B"));

            sel.ResetSelection();

            Assert.Equal(-1, sel.AnchorPageIndex);
            Assert.Equal(-1, sel.FocusPageIndex);
            Assert.Equal(-1, sel.AnchorOffset);
            Assert.Equal(-1d, sel.AnchorOffsetDistance);
            Assert.Equal(-1, sel.FocusOffset);
            Assert.Equal(-1d, sel.FocusOffsetDistance);
            Assert.Null(sel.AnchorWord);
            Assert.Null(sel.FocusWord);
            Assert.Null(sel.AnchorPoint);
            Assert.False(sel.HasStarted);
            Assert.False(sel.IsValid);
        }

        [Fact]
        public void ResetSelection_FiresTextSelectionResetEvent()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord());
            int firedCount = 0;
            sel.TextSelectionReset += (_, _) => firedCount++;

            sel.ResetSelection();

            Assert.Equal(1, firedCount);
        }

        // -----------------------------------------------------------------------
        // Selection direction
        // -----------------------------------------------------------------------

        [Fact]
        public void IsBackward_AnchorPageAfterFocusPage()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);

            sel.Start(3, a); // anchor page 3
            sel.Extend(1, f); // focus page 1 — before anchor

            Assert.True(sel.IsBackward);
            Assert.False(sel.IsForward);
        }

        [Fact]
        public void IsForward_AnchorPageBeforeFocusPage()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);

            sel.Start(1, a);
            sel.Extend(3, f);

            Assert.False(sel.IsBackward);
            Assert.True(sel.IsForward);
        }

        [Fact]
        public void IsBackward_SamePage_AnchorWordAfterFocusWord()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 5); // later in page
            var f = MakeWord("B"); SetIndexInPage(f, 2); // earlier in page

            sel.Start(1, a);
            sel.Extend(1, f);

            Assert.True(sel.IsBackward);
        }

        [Fact]
        public void IsForward_SamePage_AnchorWordBeforeFocusWord()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 2);
            var f = MakeWord("B"); SetIndexInPage(f, 5);

            sel.Start(1, a);
            sel.Extend(1, f);

            Assert.False(sel.IsBackward);
            Assert.True(sel.IsForward);
        }

        [Fact]
        public void IsBackward_SameWord_AnchorOffsetAfterFocusOffset()
        {
            var sel = new TextSelection(5);
            var word = MakeWord("HELLO", xStart: 0, width: 50);
            // anchor at x=35 → letter index 3
            sel.Start(1, word, new Point(35, 6));
            // focus at x=15 → letter index 1 (before anchor offset)
            sel.Extend(1, word, new Point(15, 6));

            Assert.True(sel.IsBackward);
        }

        // -----------------------------------------------------------------------
        // Page range helpers
        // -----------------------------------------------------------------------

        [Fact]
        public void IsPageInSelection_InsideRange_ReturnsTrue()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(2, a);
            sel.Extend(4, f);

            Assert.True(sel.IsPageInSelection(2));
            Assert.True(sel.IsPageInSelection(3));
            Assert.True(sel.IsPageInSelection(4));
        }

        [Fact]
        public void IsPageInSelection_OutsideRange_ReturnsFalse()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(2, a);
            sel.Extend(4, f);

            Assert.False(sel.IsPageInSelection(1));
            Assert.False(sel.IsPageInSelection(5));
        }

        [Fact]
        public void GetStartPageIndex_Forward_ReturnsAnchorPage()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(1, a);
            sel.Extend(3, f);

            Assert.Equal(1, sel.GetStartPageIndex());
        }

        [Fact]
        public void GetEndPageIndex_Forward_ReturnsFocusPage()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(1, a);
            sel.Extend(3, f);

            Assert.Equal(3, sel.GetEndPageIndex());
        }

        [Fact]
        public void GetStartPageIndex_Backward_ReturnsFocusPage()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(3, a);
            sel.Extend(1, f); // backward

            Assert.Equal(1, sel.GetStartPageIndex()); // focus page is earlier
        }

        [Fact]
        public void GetEndPageIndex_Backward_ReturnsAnchorPage()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(3, a);
            sel.Extend(1, f); // backward

            Assert.Equal(3, sel.GetEndPageIndex()); // anchor page is later
        }

        // -----------------------------------------------------------------------
        // GetSelectedPagesIndexes
        // -----------------------------------------------------------------------

        [Fact]
        public void GetSelectedPagesIndexes_NotValid_YieldsNothing()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord()); // no Extend yet → FocusWord null → not valid

            Assert.Empty(sel.GetSelectedPagesIndexes());
        }

        [Fact]
        public void GetSelectedPagesIndexes_SinglePage_YieldsOnePage()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 1);
            sel.Start(2, a);
            sel.Extend(2, f);

            Assert.Equal([2], sel.GetSelectedPagesIndexes());
        }

        [Fact]
        public void GetSelectedPagesIndexes_MultiPage_YieldsAllPagesInOrder()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(2, a);
            sel.Extend(4, f);

            Assert.Equal([2, 3, 4], sel.GetSelectedPagesIndexes());
        }

        [Fact]
        public void GetSelectedPagesIndexes_Backward_YieldsFromStartToEnd()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 0);
            sel.Start(4, a); // anchor on page 4
            sel.Extend(2, f); // focus on page 2 — backward

            Assert.True(sel.IsBackward);
            // Start = FocusPage(2), End = AnchorPage(4)
            Assert.Equal([2, 3, 4], sel.GetSelectedPagesIndexes());
        }

        // -----------------------------------------------------------------------
        // IsWordSelected
        // -----------------------------------------------------------------------

        [Fact]
        public void IsWordSelected_NotValid_ReturnsFalse()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord("A")); // no Extend → not valid
            var w = MakeWord("X"); SetIndexInPage(w, 0);

            Assert.False(sel.IsWordSelected(1, w));
        }

        [Fact]
        public void IsWordSelected_PageNotInSelection_ReturnsFalse()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("B"); SetIndexInPage(f, 1);
            sel.Start(2, a);
            sel.Extend(3, f);

            var w = MakeWord("X"); SetIndexInPage(w, 0);
            Assert.False(sel.IsWordSelected(1, w)); // page 1 outside [2,3]
            Assert.False(sel.IsWordSelected(4, w)); // page 4 outside [2,3]
        }

        [Fact]
        public void IsWordSelected_Forward_BoundaryWordsIncluded()
        {
            var sel = new TextSelection(5);
            var anchor = MakeWord("A"); SetIndexInPage(anchor, 2);
            var focus  = MakeWord("C"); SetIndexInPage(focus,  5);
            sel.Start(1, anchor);
            sel.Extend(1, focus);

            Assert.True(sel.IsWordSelected(1, anchor)); // left boundary
            Assert.True(sel.IsWordSelected(1, focus));  // right boundary
        }

        [Fact]
        public void IsWordSelected_Forward_MiddleWordIncluded()
        {
            var sel = new TextSelection(5);
            var anchor = MakeWord("A"); SetIndexInPage(anchor, 2);
            var focus  = MakeWord("C"); SetIndexInPage(focus,  5);
            sel.Start(1, anchor);
            sel.Extend(1, focus);

            var middle = MakeWord("B"); SetIndexInPage(middle, 3);
            Assert.True(sel.IsWordSelected(1, middle));
        }

        [Fact]
        public void IsWordSelected_Forward_OutsideRangeReturnsFalse()
        {
            var sel = new TextSelection(5);
            var anchor = MakeWord("A"); SetIndexInPage(anchor, 2);
            var focus  = MakeWord("C"); SetIndexInPage(focus,  5);
            sel.Start(1, anchor);
            sel.Extend(1, focus);

            var before = MakeWord("X"); SetIndexInPage(before, 1);
            var after  = MakeWord("Y"); SetIndexInPage(after,  6);
            Assert.False(sel.IsWordSelected(1, before));
            Assert.False(sel.IsWordSelected(1, after));
        }

        [Fact]
        public void IsWordSelected_Backward_WordInRange_ReturnsTrue()
        {
            var sel = new TextSelection(5);
            var anchor = MakeWord("A"); SetIndexInPage(anchor, 5); // later word
            var focus  = MakeWord("B"); SetIndexInPage(focus,  2); // earlier word
            sel.Start(1, anchor);
            sel.Extend(1, focus); // backward

            Assert.True(sel.IsBackward);
            var middle = MakeWord("M"); SetIndexInPage(middle, 3);
            Assert.True(sel.IsWordSelected(1, middle));
        }

        // -----------------------------------------------------------------------
        // GetPageSelectionAs
        // -----------------------------------------------------------------------

        [Fact]
        public void GetPageSelectionAs_EmptyWords_YieldsNothing()
        {
            var sel = new TextSelection(5);
            sel.Start(1, MakeWord());
            sel.Extend(1, MakeWord("B"));

            var result = sel.GetPageSelectionAs<string>(
                [],
                pageNumber: 1,
                processFull: _ => "full",
                processPartial: (_, _, _) => "partial");

            Assert.Empty(result);
        }

        [Fact]
        public void GetPageSelectionAs_SingleWord_NoOffsets_UsesProcessFull()
        {
            var sel = new TextSelection(5);
            var word = MakeWord("HELLO");
            sel.Start(1, word);  // no location → AnchorOffset = -1
            sel.Extend(1, word); // no location → FocusOffset  = -1

            var result = sel.GetPageSelectionAs<string>(
                [word],
                pageNumber: 1,
                processFull: _ => "full",
                processPartial: (_, _, _) => "partial").ToList();

            Assert.Single(result);
            Assert.Equal("full", result[0]);
        }

        [Fact]
        public void GetPageSelectionAs_SingleWord_PartialBothEnds_UsesProcessPartial()
        {
            var sel = new TextSelection(5);
            // 5-letter word "HELLO" at x=[0,50]
            var word = MakeWord("HELLO", xStart: 0, width: 50);
            sel.Start(1, word, new Point(15, 6));  // AnchorOffset = 1
            sel.Extend(1, word, new Point(35, 6)); // FocusOffset  = 3

            Assert.Equal(1, sel.AnchorOffset);
            Assert.Equal(3, sel.FocusOffset);

            (PdfWord? w, int s, int e) captured = (null, -1, -1);
            var result = sel.GetPageSelectionAs<string>(
                [word],
                pageNumber: 1,
                processFull: _ => "full",
                processPartial: (w, s, e) => { captured = (w, s, e); return "partial"; }).ToList();

            Assert.Single(result);
            Assert.Equal("partial", result[0]);
            Assert.Same(word, captured.w);
            Assert.Equal(1, captured.s);
            Assert.Equal(3, captured.e);
        }

        [Fact]
        public void GetPageSelectionAs_SingleWord_StartAtIndex0_UsesProcessFull()
        {
            var sel = new TextSelection(5);
            var word = MakeWord("HELLO", xStart: 0, width: 50);
            // x=5 is in the first letter (letter 0)
            sel.Start(1, word, new Point(5, 6));   // AnchorOffset = 0
            sel.Extend(1, word, new Point(45, 6)); // FocusOffset  = 4 (last)

            Assert.Equal(0, sel.AnchorOffset);

            var result = sel.GetPageSelectionAs<string>(
                [word],
                pageNumber: 1,
                processFull: _ => "full",
                processPartial: (_, _, _) => "partial").ToList();

            // wordStartIndex=0 satisfies (wordStartIndex == -1 || wordStartIndex == 0)
            // wordEndIndex=4 = lastIndex(4) → satisfies (wordEndIndex == lastIndex)
            // → processFull
            Assert.Single(result);
            Assert.Equal("full", result[0]);
        }

        [Fact]
        public void GetPageSelectionAs_MultipleWords_NoOffsets_AllFull()
        {
            var sel = new TextSelection(5);
            var a = MakeWord("A"); SetIndexInPage(a, 0);
            var f = MakeWord("C"); SetIndexInPage(f, 2);
            sel.Start(1, a); // AnchorOffset = -1
            sel.Extend(1, f); // FocusOffset  = -1

            var words = new List<PdfWord> { MakeWord("A"), MakeWord("B"), MakeWord("C") };
            var result = sel.GetPageSelectionAs<string>(
                words,
                pageNumber: 1,
                processFull: _ => "full",
                processPartial: (_, _, _) => "partial").ToList();

            Assert.Equal(3, result.Count);
            Assert.All(result, r => Assert.Equal("full", r));
        }

        [Fact]
        public void GetPageSelectionAs_MultipleWords_BothOffsets_FirstAndLastPartial()
        {
            var sel = new TextSelection(5);
            var anchorWord = MakeWord("HELLO", xStart: 0, width: 50);
            var focusWord  = MakeWord("WORLD", xStart: 0, width: 50);
            // x=15 → letter index 1 (AnchorOffset = 1, not 0 → first word is partial)
            sel.Start(1, anchorWord, new Point(15, 6));
            // x=35 → letter index 3 (FocusOffset = 3, not last(4) → last word is partial)
            sel.Extend(1, focusWord, new Point(35, 6));

            var middleWord = MakeWord("MID");
            var callLog = new List<string>();

            var result = sel.GetPageSelectionAs<string>(
                [anchorWord, middleWord, focusWord],
                pageNumber: 1,
                processFull: w =>
                {
                    callLog.Add($"full:{w.Value}");
                    return "full";
                },
                processPartial: (w, s, e) =>
                {
                    callLog.Add($"partial:{w.Value}[{s},{e}]");
                    return "partial";
                }).ToList();

            Assert.Equal(3, result.Count);
            // first word: AnchorOffset=1 → partial from 1 to last letter (4)
            Assert.Contains(callLog, c => c == "partial:HELLO[1,4]");
            // middle word: always full
            Assert.Contains(callLog, c => c == "full:MID");
            // last word: FocusOffset=3 → partial from 0 to 3
            Assert.Contains(callLog, c => c == "partial:WORLD[0,3]");
        }

        [Fact]
        public void GetPageSelectionAs_MultipleWords_OnlyStartOffset_FirstPartialRestFull()
        {
            var sel = new TextSelection(5);
            var anchorWord = MakeWord("HELLO", xStart: 0, width: 50);
            var focusWord  = MakeWord("WORLD");
            // anchor starts at letter 2 (partial), focus has no location (full)
            sel.Start(1, anchorWord, new Point(25, 6)); // AnchorOffset = 2
            sel.Extend(1, focusWord);                   // FocusOffset  = -1

            var words = new List<PdfWord> { anchorWord, focusWord };
            var callLog = new List<string>();

            sel.GetPageSelectionAs<string>(
                words,
                pageNumber: 1,
                processFull: w => { callLog.Add($"full:{w.Value}"); return "full"; },
                processPartial: (w, s, e) => { callLog.Add($"partial:{w.Value}[{s},{e}]"); return "partial"; })
                .ToList();

            Assert.Contains(callLog, c => c.StartsWith("partial:HELLO"));
            Assert.Contains(callLog, c => c == "full:WORLD");
        }

        [Fact]
        public void GetPageSelectionAs_MultipleWords_OnlyEndOffset_LastPartialRestFull()
        {
            var sel = new TextSelection(5);
            var anchorWord = MakeWord("HELLO");
            var focusWord  = MakeWord("WORLD", xStart: 0, width: 50);
            sel.Start(1, anchorWord);                   // AnchorOffset = -1
            sel.Extend(1, focusWord, new Point(25, 6)); // FocusOffset  = 2

            var words = new List<PdfWord> { anchorWord, focusWord };
            var callLog = new List<string>();

            sel.GetPageSelectionAs<string>(
                words,
                pageNumber: 1,
                processFull: w => { callLog.Add($"full:{w.Value}"); return "full"; },
                processPartial: (w, s, e) => { callLog.Add($"partial:{w.Value}[{s},{e}]"); return "partial"; })
                .ToList();

            Assert.Contains(callLog, c => c == "full:HELLO");
            Assert.Contains(callLog, c => c.StartsWith("partial:WORLD"));
        }

        [Fact]
        public void GetPageSelectionAs_Backward_OffsetsAreSwapped()
        {
            var sel = new TextSelection(5);
            var anchorWord = MakeWord("HELLO", xStart: 0, width: 50);
            var focusWord  = MakeWord("WORLD", xStart: 0, width: 50);
            // Backward: anchor is on a LATER page, focus on an EARLIER page
            sel.Start(2, anchorWord, new Point(35, 6)); // AnchorOffset = 3
            sel.Extend(1, focusWord, new Point(15, 6)); // FocusOffset  = 1

            Assert.True(sel.IsBackward);

            // When backward and we call GetPageSelectionAs for page 1 (focus page):
            // wordStartIndex = FocusOffset  = 1  (focus is the start in reading order)
            // wordEndIndex   = AnchorOffset = -1 → because AnchorPageIndex(2) != pageNumber(1)
            var callLog = new List<string>();
            sel.GetPageSelectionAs<string>(
                [focusWord],
                pageNumber: 1,
                processFull: w => { callLog.Add($"full:{w.Value}"); return "full"; },
                processPartial: (w, s, e) => { callLog.Add($"partial:{w.Value}[{s},{e}]"); return "partial"; })
                .ToList();

            // focusWord starts at offset 1 with full end → partial
            Assert.Contains(callLog, c => c.StartsWith("partial:WORLD"));
        }
    }
}
