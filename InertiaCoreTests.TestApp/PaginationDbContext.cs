using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace InertiaCoreTests.TestApp
{
    /// <summary>
    /// Simple in-memory entity used for pagination integration tests.
    /// </summary>
    public sealed class Todo
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public bool Done { get; set; }
    }

    /// <summary>
    /// DB context using an in-memory SQLite provider for integration testing.
    /// </summary>
    public sealed class PaginationDbContext : DbContext
    {
        public DbSet<Todo> Todos => Set<Todo>();

        public PaginationDbContext(DbContextOptions<PaginationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuildingContext modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed with a predictable set of 25 items.
            modelBuilder.Entity<Todo>().HasData(
                Enumerable.Range(1, 25).Select(i => new Todo
                {
                    Id = i,
                    Title = $"Todo {i:D2}",
                    Done = i % 2 == 0
                })
            );
        }
    }
}
