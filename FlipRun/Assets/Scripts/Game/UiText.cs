using System.Linq;

namespace FlipRun.Game
{
    /// <summary>
    /// 画面に出す文字をすべてここに集める。
    /// フォントには、ここに出てくる文字（＋半角英数字・記号）だけを入れている（FlipRun > Setup UI Font）。
    /// 文字を足したら、メニューからフォントを作り直すこと。
    /// </summary>
    public static class UiText
    {
        public const string FontResource = "Fonts/NotoSansJP-Regular";

        public const string Logo = "FLIP RUN";
        public const string TitleHint = "クリック / スペース でスタート";
        public const string HighScore = "ハイスコア";
        public const string Distance = "距離";
        public const string Coins = "コイン";
        public const string Score = "スコア";
        public const string New = "NEW!";
        public const string RetryHint = "スペース / クリック / R でリトライ";
        public const string CoinMark = "●";
        public const string Meter = "m";
        public const string NearMiss = "ニアミス！";
        public const string NearMissBonus = "+5";
        public const string SoundOn = "音 ON";
        public const string SoundOff = "音 OFF";

        public static readonly string[] All =
        {
            Logo, TitleHint, HighScore, Distance, Coins, Score, New, RetryHint, CoinMark, Meter,
            NearMiss, NearMissBonus, SoundOn, SoundOff,
        };

        /// <summary>半角の英数字・記号（0x20〜0x7E）。</summary>
        public static string Ascii => new string(Enumerable.Range(0x20, 0x7F - 0x20).Select(i => (char)i).ToArray());

        /// <summary>フォントに入れる文字の一覧（重複なし）。</summary>
        public static string FontCharacters =>
            new string((Ascii + string.Concat(All)).Distinct().OrderBy(c => c).ToArray());
    }
}
