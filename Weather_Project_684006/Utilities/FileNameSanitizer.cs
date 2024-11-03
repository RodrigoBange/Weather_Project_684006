namespace Weather_Project_684006.Utilities;

public class FileNameSanitizer
{
    public static string Sanitize(string input)
    {
        // Replace all spaces with dashes
        input = input.Replace(' ', '-');

        // Replace any invalid file name characters with dashes
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '-');
        }

        return input;
    }
}