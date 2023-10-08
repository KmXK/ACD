using ACD.Infrastructure;

namespace ACD.Parser;

public interface IParser
{
    public Model? Parse(IEnumerable<string> parseData);
}