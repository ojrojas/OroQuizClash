using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

using OroQuizClash.Domain.Authorization;

namespace OroQuizClash.Api.Authorization;

public static class SecurityPolicies
{
    public const string CategoryRead = "Category.Read";
    public const string CategoryWrite = "Category.Write";
    public const string CategoryPublish = "Category.Publish";
    public const string QuestionRead = "Question.Read";
    public const string QuestionWrite = "Question.Write";
    public const string QuestionPublish = "Question.Publish";
    public const string GameCreate = "Game.Create";
    public const string GameStart = "Game.Start";
    public const string GamePlay = "Game.Play";
    public const string RewardRead = "Reward.Read";
    public const string RewardRedeem = "Reward.Redeem";
    public const string RewardManage = "Reward.Manage";
    public const string ReportRead = "Report.Read";
    public const string AuditRead = "Audit.Read";

    public static readonly IReadOnlyDictionary<string, string[]> PolicyRoles = new Dictionary<string, string[]>
    {
        [CategoryRead] = ["ADMIN", "GAME_MANAGER", "PLAYER"],
        [CategoryWrite] = ["ADMIN", "GAME_MANAGER"],
        [CategoryPublish] = ["ADMIN", "GAME_MANAGER"],
        [QuestionRead] = ["ADMIN", "GAME_MANAGER"],
        [QuestionWrite] = ["ADMIN", "GAME_MANAGER"],
        [QuestionPublish] = ["ADMIN", "GAME_MANAGER"],
        [GameCreate] = ["ADMIN", "GAME_MANAGER"],
        [GameStart] = ["ADMIN", "GAME_MANAGER"],
        [GamePlay] = ["ADMIN", "GAME_MANAGER", "PLAYER"],
        [RewardRead] = ["ADMIN", "GAME_MANAGER", "PLAYER", "REWARD_MANAGER"],
        [RewardRedeem] = ["ADMIN", "PLAYER"],
        [RewardManage] = ["ADMIN", "REWARD_MANAGER"],
        [ReportRead] = ["ADMIN", "GAME_MANAGER", "REWARD_MANAGER"],
        [AuditRead] = ["ADMIN"]
    };

    public static AuthorizationBuilder AddSecurityPolicies(this AuthorizationBuilder builder)
    {
        foreach (var kvp in PolicyRoles)
        {
            var permission = kvp.Key;
            var roles = kvp.Value;
            builder.AddPolicy(permission, policy =>
                policy.RequireAssertion(ctx =>
                    roles.Any(role =>
                        ctx.User.HasClaim(c => c.Type == "roles" && c.Value == role) ||
                        ctx.User.HasClaim(c => c.Type == "role" && c.Value == role) ||
                        ctx.User.IsInRole(role))));
        }

        // Keep legacy policies for backward compat
        builder.AddPolicy("AdminOrGameManager", policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.HasClaim(c => c.Type == "roles" && (c.Value == "ADMIN" || c.Value == "GAME_MANAGER")) ||
                ctx.User.HasClaim(c => c.Type == "role" && (c.Value == "ADMIN" || c.Value == "GAME_MANAGER")) ||
                ctx.User.IsInRole("ADMIN") || ctx.User.IsInRole("GAME_MANAGER")));
        builder.AddPolicy("AdminOrRewardManager", policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.HasClaim(c => c.Type == "roles" && (c.Value == "ADMIN" || c.Value == "REWARD_MANAGER")) ||
                ctx.User.HasClaim(c => c.Type == "role" && (c.Value == "ADMIN" || c.Value == "REWARD_MANAGER")) ||
                ctx.User.IsInRole("ADMIN") || ctx.User.IsInRole("REWARD_MANAGER")));

        return builder;
    }
}
