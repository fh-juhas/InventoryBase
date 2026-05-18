using System.ComponentModel.DataAnnotations;

namespace InventoryBase.Web.ViewModels
{
    public class CreateUserViewModel
    {
        [Required] public string FullName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
        [Required] public string Role { get; set; } = "User";
    }

    public class UserListViewModel
    {
        public string Hash { get; set; } = string.Empty;  // encoded RowId — used in URLs
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
