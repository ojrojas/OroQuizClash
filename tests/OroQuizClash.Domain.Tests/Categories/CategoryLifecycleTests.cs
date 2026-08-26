using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Categories.Events;
using OroQuizClash.Domain.Categories.ValueObjects;
using OroQuizClash.Infrastructure.Counters;

namespace OroQuizClash.Domain.Tests.Categories;

public sealed class CategoryLifecycleTests
{
    private static (KnowledgeArea ka, AcademicLevel al, AgeRange ar, DifficultyLevel dl, CategoryTags tags, PublishConfiguration pc) ValidVOs()
    {
        var ka = new KnowledgeArea("Humanidades");
        var al = new AcademicLevel("Secundaria");
        var ar = new AgeRange(13, 17);
        var dl = new DifficultyLevel(3);
        var tg = new CategoryTags(["historia", "secundaria"]);
        var pc = new PublishConfiguration(false);
        return (ka, al, ar, dl, tg, pc);
    }

    private static Category CreateDraft(string name = "Historia Universal")
    {
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        var result = Category.Create(name, "desc", ka, al, ar, dl, tags, pc, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        // Clear creation event to isolate lifecycle events
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    [Fact]
    public async Task Publish_With0Valid_Fails_CategoryNotPublishable_RemainsDRAFT()
    {
        // Arrange
        var category = CreateDraft();
        var counter = new InMemoryQuestionCounter();
        counter.Seed(category.Id, 0);

        // Act
        var result = await category.PublishAsync(counter);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("CategoryNotPublishable", result.Error.Code);
        Assert.Equal(CategoryStatus.Draft, category.Status);
        Assert.Empty(category.DomainEvents);
    }

    [Fact]
    public async Task Publish_With4Valid_Fails_CategoryNotPublishable()
    {
        // Arrange
        var category = CreateDraft();
        var counter = new InMemoryQuestionCounter();
        counter.Seed(category.Id, 4);

        // Act
        var result = await category.PublishAsync(counter);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("CategoryNotPublishable", result.Error.Code);
        Assert.Equal(CategoryStatus.Draft, category.Status);
    }

    [Fact]
    public async Task Publish_With5Valid_Succeeds_ACTIVE_AndRaisesEvent()
    {
        // Arrange
        var category = CreateDraft();
        var counter = new InMemoryQuestionCounter();
        counter.Seed(category.Id, 5);

        // Act
        var result = await category.PublishAsync(counter);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Active, category.Status);
        Assert.Contains(category.DomainEvents, e => e is CategoryPublishedDomainEvent);
        var ev = category.DomainEvents.OfType<CategoryPublishedDomainEvent>().Single();
        Assert.Equal(category.Id.Value, ev.CategoryId);
    }

    [Fact]
    public async Task Publish_MisalignedQuestions_NotCounted_FR007()
    {
        // Arrange: 3 valid + 2 misaligned = only 3 counted => fail; then add 2 more valid => 5 counted => success
        var category = CreateDraft();
        var counter = new InMemoryQuestionCounter();
        // Seed 3 valid
        counter.Seed(category.Id, 3);
        counter.AddQuestion(QuestionStub.CreateMisaligned(category.Id));
        counter.AddQuestion(QuestionStub.CreateMisaligned(category.Id));

        // Act first
        var result1 = await category.PublishAsync(counter);

        // Assert 3 counted
        Assert.True(result1.IsFailure);
        Assert.Equal("CategoryNotPublishable", result1.Error.Code);

        // Arrange second: clear and seed 5 valid + 2 misaligned (misaligned not counted)
        counter.Clear(category.Id);
        counter.Seed(category.Id, 5);
        counter.AddQuestion(QuestionStub.CreateMisaligned(category.Id));
        counter.AddQuestion(QuestionStub.CreateMisaligned(category.Id));
        category.ClearDomainEvents();

        // Act second
        var result2 = await category.PublishAsync(counter);

        // Assert 5 counted, misaligned ignored
        Assert.True(result2.IsSuccess);
        Assert.Equal(CategoryStatus.Active, category.Status);
    }

    [Fact]
    public async Task Publish_InvalidQuestionsDistribution_NotCounted()
    {
        // Arrange: 5 stubs but each invalid type, then 5 valid should succeed
        var category = CreateDraft();
        var counter = new InMemoryQuestionCounter();
        counter.AddQuestion(QuestionStub.CreateInvalidOptions(category.Id)); // 3 opts
        counter.AddQuestion(QuestionStub.CreateInvalidCorrect(category.Id, 0)); // 0 correct
        counter.AddQuestion(QuestionStub.CreateInvalidCorrect(category.Id, 2)); // 2 correct
        counter.AddQuestion(QuestionStub.CreateInactive(category.Id)); // inactive
        counter.AddQuestion(QuestionStub.CreateMisaligned(category.Id)); // desalineada

        var resultInvalid = await category.PublishAsync(counter);
        Assert.True(resultInvalid.IsFailure);
        Assert.Equal("CategoryNotPublishable", resultInvalid.Error.Code);

        // Now add 5 valid
        counter.Clear(category.Id);
        counter.Seed(category.Id, 5);
        var resultValid = await category.PublishAsync(counter);
        Assert.True(resultValid.IsSuccess);
    }

    [Fact]
    public async Task Publish_FromINACTIVE_With5Valid_Succeeds()
    {
        // Arrange: DRAFT -> activate -> deactivate -> INACTIVE -> publish
        var category = CreateDraft();
        var counter = new InMemoryQuestionCounter();
        counter.Seed(category.Id, 5);

        // Activate then Deactivate to get INACTIVE
        Assert.True(category.Activate().IsSuccess);
        Assert.True(category.Deactivate().IsSuccess);
        Assert.Equal(CategoryStatus.Inactive, category.Status);
        category.ClearDomainEvents();

        // Act
        var result = await category.PublishAsync(counter);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Active, category.Status);
        Assert.Contains(category.DomainEvents, e => e is CategoryPublishedDomainEvent);
    }

    [Fact]
    public async Task Publish_FromARCHIVED_Fails_InvalidCategoryState()
    {
        // Arrange
        var category = CreateDraft();
        category.Archive();
        Assert.Equal(CategoryStatus.Archived, category.Status);
        category.ClearDomainEvents();
        var counter = new InMemoryQuestionCounter();
        counter.Seed(category.Id, 5);

        // Act
        var result = await category.PublishAsync(counter);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
        Assert.Equal(CategoryStatus.Archived, category.Status);
        Assert.DoesNotContain(category.DomainEvents, e => e is CategoryPublishedDomainEvent);
    }

    [Fact]
    public void Activate_FromDRAFT_Succeeds()
    {
        // Arrange
        var category = CreateDraft();
        // Act
        var result = category.Activate();
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Active, category.Status);
    }

    [Fact]
    public void Activate_FromINACTIVE_Succeeds()
    {
        // Arrange
        var category = CreateDraft();
        category.Activate();
        category.Deactivate();
        Assert.Equal(CategoryStatus.Inactive, category.Status);
        // Act
        var result = category.Activate();
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Active, category.Status);
    }

    [Fact]
    public void Activate_FromACTIVE_Fails()
    {
        // Arrange
        var category = CreateDraft();
        category.Activate();
        // Act
        var result = category.Activate();
        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
    }

    [Fact]
    public void Activate_FromARCHIVED_Fails()
    {
        // Arrange
        var category = CreateDraft();
        category.Archive();
        // Act
        var result = category.Activate();
        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
    }

    [Fact]
    public void Deactivate_FromACTIVE_Succeeds()
    {
        // Arrange
        var category = CreateDraft();
        category.Activate();
        // Act
        var result = category.Deactivate();
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Inactive, category.Status);
    }

    [Fact]
    public void Deactivate_FromDRAFT_Fails()
    {
        var category = CreateDraft();
        var result = category.Deactivate();
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
    }

    [Fact]
    public void Deactivate_FromINACTIVE_Fails()
    {
        var category = CreateDraft();
        category.Activate();
        category.Deactivate();
        var result = category.Deactivate();
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
    }

    [Fact]
    public void Deactivate_FromARCHIVED_Fails()
    {
        var category = CreateDraft();
        category.Archive();
        var result = category.Deactivate();
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
    }

    [Fact]
    public void Archive_FromDRAFT_Succeeds_AndRaisesEvent()
    {
        var category = CreateDraft();
        var result = category.Archive();
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Archived, category.Status);
        Assert.Contains(category.DomainEvents, e => e is CategoryArchivedDomainEvent);
    }

    [Fact]
    public void Archive_FromACTIVE_Succeeds()
    {
        var category = CreateDraft();
        category.Activate();
        category.ClearDomainEvents();
        var result = category.Archive();
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Archived, category.Status);
        Assert.Contains(category.DomainEvents, e => e is CategoryArchivedDomainEvent);
    }

    [Fact]
    public void Archive_FromINACTIVE_Succeeds()
    {
        var category = CreateDraft();
        category.Activate();
        category.Deactivate();
        category.ClearDomainEvents();
        var result = category.Archive();
        Assert.True(result.IsSuccess);
        Assert.Equal(CategoryStatus.Archived, category.Status);
    }

    [Fact]
    public void Archive_FromARCHIVED_Fails()
    {
        var category = CreateDraft();
        category.Archive();
        category.ClearDomainEvents();
        var result = category.Archive();
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
        Assert.Empty(category.DomainEvents);
    }

    [Fact]
    public void FullLifecycle_ACTIVE_Deactivate_INACTIVE_Archived()
    {
        // Arrange DRAFT with 5 valid -> Publish ACTIVE
        var category = CreateDraft();
        var counter = new InMemoryQuestionCounter();
        counter.Seed(category.Id, 5);
        // Act lifecycle
        var pub = category.PublishAsync(counter).GetAwaiter().GetResult();
        Assert.True(pub.IsSuccess);
        Assert.Equal(CategoryStatus.Active, category.Status);
        var deact = category.Deactivate();
        Assert.True(deact.IsSuccess);
        Assert.Equal(CategoryStatus.Inactive, category.Status);
        var arch = category.Archive();
        Assert.True(arch.IsSuccess);
        Assert.Equal(CategoryStatus.Archived, category.Status);
        // ARCHIVED -> Publish should fail
        counter.Clear();
        counter.Seed(category.Id, 5);
        var pub2 = category.PublishAsync(counter).GetAwaiter().GetResult();
        Assert.True(pub2.IsFailure);
        Assert.Equal("InvalidCategoryState", pub2.Error.Code);
    }
}