namespace ebuild.cli
{
    /// <summary>
    /// Converter interface for converting a string into a value used by option/argument fields.
    /// Implement custom converters by implementing this interface.
    /// </summary>
    public interface IStringConverter
    {
        object Convert(string value);
    }
}
