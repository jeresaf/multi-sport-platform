using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class IntegrationEventNameTests
{
    [Fact]
    public void Name_is_the_declaring_class_and_version_the_record_name()
    {
        IntegrationEventName.Of(typeof(ResultApproved.V1)).ShouldBe(new IntegrationEventName("ResultApproved", 1));
        IntegrationEventName.Of(typeof(ResultApproved.V12)).ShouldBe(new IntegrationEventName("ResultApproved", 12));
        IntegrationEventName.Of(typeof(ResultApproved.V12)).ToString().ShouldBe("ResultApproved.V12");
    }

    [Theory]
    [InlineData(typeof(TopLevelApproved))]
    [InlineData(typeof(ResultApproved.Latest))]
    [InlineData(typeof(ResultApproved.V0))]
    [InlineData(typeof(ResultApproved.V1Draft))]
    [InlineData(typeof(NotStatic.V1))]
    [InlineData(typeof(ResultApproved.NotAnEvent))]
    public void Rejects_types_that_do_not_follow_the_convention(Type eventType)
    {
        Should.Throw<ArgumentException>(() => IntegrationEventName.Of(eventType));
    }

    private static class ResultApproved
    {
        public sealed record V1 : IIntegrationEvent;

        public sealed record V12 : IIntegrationEvent;

        public sealed record V0 : IIntegrationEvent;

        public sealed record V1Draft : IIntegrationEvent;

        public sealed record Latest : IIntegrationEvent;

        public sealed record NotAnEvent;
    }

    private sealed class NotStatic
    {
        public sealed record V1 : IIntegrationEvent;
    }

    private sealed record TopLevelApproved : IIntegrationEvent;
}
