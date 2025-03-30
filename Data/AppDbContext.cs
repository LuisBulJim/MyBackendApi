using Microsoft.EntityFrameworkCore;
using MyBackendApi.Models;

namespace MyBackendApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Image> Images { get; set; }
    }
}
