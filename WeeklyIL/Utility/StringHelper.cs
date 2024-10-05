using System.Text.RegularExpressions;

namespace WeeklyIL.Utility;

public static partial class StringHelper
{
    public static Uri? GetUriFromString(this string str)
    {
        Match match = UriRegex().Match(str);
        return match.Success ? new Uri(match.Value) : null;
    }

    [GeneratedRegex(@"(http|https):\/\/([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:\/~+#-]*[\w@?^=%&\/~+#-])")]
    private static partial Regex UriRegex();
}