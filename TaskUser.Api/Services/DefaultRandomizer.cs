namespace TaskUser.Api.Services
{
    public class DefaultRandomizer : IRandomizer
{
    private readonly Random _rng = Random.Shared;
    public int Next(int maxExclusive) => _rng.Next(maxExclusive);
}
}