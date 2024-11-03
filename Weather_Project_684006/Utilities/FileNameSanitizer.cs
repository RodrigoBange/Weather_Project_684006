namespace Weather_Project_684006.Utilities;

public class FileNameSanitizer
{
    public static string Sanitize(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '-');
        }
        return input;
    }
}