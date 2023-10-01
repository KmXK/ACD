namespace ACD.Parser;

public interface IParser
{
    public ParseResult Parse(IEnumerable<string> parseData);
}