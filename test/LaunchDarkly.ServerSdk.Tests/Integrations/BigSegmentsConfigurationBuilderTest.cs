﻿using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class BigSegmentsConfigurationBuilderTest : BaseTest
    {
        private readonly IBigSegmentStore _store;
        private readonly IBigSegmentStoreFactory _storeFactory;
        private readonly BuilderInternalTestUtil<BigSegmentsConfigurationBuilder> _tester;

        public BigSegmentsConfigurationBuilderTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            var storeMock = new Mock<IBigSegmentStore>();
            _store = storeMock.Object;
            var storeFactoryMock = new Mock<IBigSegmentStoreFactory>();
            _storeFactory = storeFactoryMock.Object;
            storeFactoryMock.Setup(f => f.CreateBigSegmentStore(basicContext)).Returns(_store);

            _tester = BuilderTestUtil.For(() => Components.BigSegments(_storeFactory));
        }

        [Fact]
        public void Store()
        {
            Assert.Same(_store, Components.BigSegments(_storeFactory).CreateBigSegmentsConfiguration(basicContext).Store);
        }

        [Fact]
        public void UserCacheSize()
        {
            var prop = _tester.Property(b => b.CreateBigSegmentsConfiguration(basicContext).UserCacheSize,
                (b, v) => b.UserCacheSize(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultUserCacheSize);
            prop.AssertCanSet(3333);
        }

        [Fact]
        public void UserCacheTime()
        {
            var prop = _tester.Property(b => b.CreateBigSegmentsConfiguration(basicContext).UserCacheTime,
                (b, v) => b.UserCacheTime(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultUserCacheTime);
            prop.AssertCanSet(TimeSpan.FromMilliseconds(3333));
        }

        [Fact]
        public void StatusPollInterval()
        {
            var prop = _tester.Property(b => b.CreateBigSegmentsConfiguration(basicContext).StatusPollInterval,
                (b, v) => b.StatusPollInterval(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultStatusPollInterval);
            prop.AssertCanSet(TimeSpan.FromMilliseconds(3333));
            prop.AssertSetIsChangedTo(TimeSpan.Zero, BigSegmentsConfigurationBuilder.DefaultStatusPollInterval);
            prop.AssertSetIsChangedTo(TimeSpan.FromMilliseconds(-1), BigSegmentsConfigurationBuilder.DefaultStatusPollInterval);
        }

        [Fact]
        public void StaleAfter()
        {
            var prop = _tester.Property(b => b.CreateBigSegmentsConfiguration(basicContext).StaleAfter,
                (b, v) => b.StaleAfter(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultStaleAfter);
            prop.AssertCanSet(TimeSpan.FromMilliseconds(3333));
            prop.AssertSetIsChangedTo(TimeSpan.Zero, BigSegmentsConfigurationBuilder.DefaultStaleAfter);
            prop.AssertSetIsChangedTo(TimeSpan.FromMilliseconds(-1), BigSegmentsConfigurationBuilder.DefaultStaleAfter);
        }
    }
}
