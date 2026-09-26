using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests for <see cref="StringExtensions"/> covering operations BCL does not provide:
    /// out-parameter split and conditional prefix / suffix removal. All comparisons default
    /// to case-insensitive (the framework convention).
    /// </summary>
    public class StringExtensionsTests
    {
        // ---- SplitLeft / SplitRight ----

        [Fact]
        [DisplayName("SplitLeft returns the left and right parts when the delimiter is found")]
        public void SplitLeft_SplitsIntoLeftAndRight()
        {
            "alpha-beta-gamma".SplitLeft("-", out var left, out var right);
            Assert.Equal("alpha", left);
            Assert.Equal("beta-gamma", right);
        }

        [Fact]
        [DisplayName("SplitLeft returns two empty strings when the delimiter is not found")]
        public void SplitLeft_NotFound_ReturnsEmpty()
        {
            "alpha".SplitLeft("-", out var left, out var right);
            Assert.Equal(string.Empty, left);
            Assert.Equal(string.Empty, right);
        }

        [Fact]
        [DisplayName("SplitRight splits at the last delimiter into left and right parts")]
        public void SplitRight_SplitsAtLastDelimiter()
        {
            "alpha-beta-gamma".SplitRight("-", out var left, out var right);
            Assert.Equal("alpha-beta", left);
            Assert.Equal("gamma", right);
        }

        // ---- LeftCut / RightCut / LeftRightCut (case-insensitive) ----

        [Fact]
        [DisplayName("LeftCut removes a matching prefix case-insensitively and otherwise returns the input unchanged")]
        public void LeftCut_ByPrefix_CaseInsensitive()
        {
            Assert.Equal("cde", "abcde".LeftCut("AB"));
            Assert.Equal("abcde", "abcde".LeftCut("XY"));
            Assert.Equal(string.Empty, ((string?)null).LeftCut("x"));
        }

        [Fact]
        [DisplayName("RightCut removes a matching suffix case-insensitively and otherwise returns the input unchanged")]
        public void RightCut_BySuffix_CaseInsensitive()
        {
            Assert.Equal("abc", "abcDE".RightCut("de"));
            Assert.Equal("abcde", "abcde".RightCut("XY"));
            Assert.Equal(string.Empty, ((string?)null).RightCut("x"));
        }

        [Fact]
        [DisplayName("LeftRightCut removes both the prefix and the suffix")]
        public void LeftRightCut_RemovesBoth()
        {
            Assert.Equal("abc", "[abc]".LeftRightCut("[", "]"));
        }
    }
}
