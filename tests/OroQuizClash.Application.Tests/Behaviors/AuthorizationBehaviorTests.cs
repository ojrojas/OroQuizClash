using System.Security.Claims;

using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using OroQuizClash.Application.Authorization;
using OroQuizClash.Application.Behaviors;
using OroQuizClash.Domain.Authorization;

namespace OroQuizClash.Application.Tests.Behaviors;

[RequiresPermission("Category.Read")]
public sealed record TestCategoryReadQuery : BuildingBlocks.CQRS.Abstractions.IQuery<Result<string>>;

[RequiresPermission("Audit.Read")]
public sealed record TestAuditQuery : BuildingBlocks.CQRS.Abstractions.IQuery<Result<string>>;

public sealed record NoPermissionQuery : BuildingBlocks.CQRS.Abstractions.IQuery<Result<string>>;

public sealed class AuthorizationBehaviorTests
{
    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        var claims = roles.Select(r => new Claim("role", r)).ToList();
        claims.Add(new Claim("sub", Guid.NewGuid().ToString()));
        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private static HttpContext HttpContextWithUser(ClaimsPrincipal user)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = user;
        return ctx;
    }

    [Fact]
    public async Task Allows_WhenUserHasPermission()
    {
        var user = PrincipalWithRoles("PLAYER");
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns(HttpContextWithUser(user));
        var behavior = new AuthorizationBehavior<TestCategoryReadQuery, Result<string>>(httpAccessor);
        var request = new TestCategoryReadQuery();

        var result = await behavior.HandleAsync(request, _ => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Denies_WhenUserLacksPermission()
    {
        var user = PrincipalWithRoles("PLAYER");
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns(HttpContextWithUser(user));
        var behavior = new AuthorizationBehavior<TestAuditQuery, Result<string>>(httpAccessor);
        var request = new TestAuditQuery();

        var result = await behavior.HandleAsync(request, _ => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task Allows_WhenNoPermissionRequired()
    {
        var user = PrincipalWithRoles("PLAYER");
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns(HttpContextWithUser(user));
        var behavior = new AuthorizationBehavior<NoPermissionQuery, Result<string>>(httpAccessor);
        var request = new NoPermissionQuery();

        var result = await behavior.HandleAsync(request, _ => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Denies_WhenUnauthenticated()
    {
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var anon = new ClaimsPrincipal(new ClaimsIdentity());
        httpAccessor.HttpContext.Returns(HttpContextWithUser(anon));
        var behavior = new AuthorizationBehavior<TestCategoryReadQuery, Result<string>>(httpAccessor);
        var request = new TestCategoryReadQuery();

        var result = await behavior.HandleAsync(request, _ => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task Admin_HasAllPermissions()
    {
        var user = PrincipalWithRoles("ADMIN");
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns(HttpContextWithUser(user));
        var behavior = new AuthorizationBehavior<TestAuditQuery, Result<string>>(httpAccessor);
        var request = new TestAuditQuery();

        var result = await behavior.HandleAsync(request, _ => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
