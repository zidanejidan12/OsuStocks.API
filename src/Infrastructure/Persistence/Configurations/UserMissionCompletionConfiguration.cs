using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class UserMissionCompletionConfiguration : IEntityTypeConfiguration<UserMissionCompletion>
{
    public void Configure(EntityTypeBuilder<UserMissionCompletion> builder)
    {
        builder.ToTable("user_mission_completions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.MissionCode).HasColumnName("mission_code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.PeriodKey).HasColumnName("period_key").HasMaxLength(16).IsRequired();
        builder.Property(x => x.RewardCredits).HasColumnName("reward_credits").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").IsRequired();

        builder.HasIndex(x => new { x.UserId, x.MissionCode, x.PeriodKey })
            .IsUnique()
            .HasDatabaseName("uq_user_mission_completion_user_code_period");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
