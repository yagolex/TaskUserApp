namespace TaskUser.Api.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = default!;

        public bool IsActive { get; set; } = true; // we can take user out from the rotation
    }
}
