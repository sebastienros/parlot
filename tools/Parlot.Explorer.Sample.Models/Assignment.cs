using System.Globalization;
using System.Resources;

namespace Parlot.Explorer.Sample;

public sealed record Assignment(string Name, int Value)
{
    // Exercises a satellite assembly loaded from a culture subdirectory in the worker snapshot.
    public string? Greeting => new ResourceManager("Parlot.Explorer.Sample.Models.Messages", typeof(Assignment).Assembly)
        .GetString("Greeting", CultureInfo.GetCultureInfo("fr"));
}
