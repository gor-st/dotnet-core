﻿using System;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.TestHelpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    public class BigSegmentsStoreWrapperTest : BaseTest
    {
        private readonly Mock<IBigSegmentStore> _storeMock;
        private readonly IBigSegmentStore _store;
        private readonly IBigSegmentStoreFactory _storeFactory;
        private readonly TaskExecutor _taskExecutor;

        public BigSegmentsStoreWrapperTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _storeMock = new Mock<IBigSegmentStore>();
            _store = _storeMock.Object;
            var storeFactoryMock = new Mock<IBigSegmentStoreFactory>();
            _storeFactory = storeFactoryMock.Object;
            storeFactoryMock.Setup(f => f.CreateBigSegmentStore(basicContext)).Returns(_store);

            _taskExecutor = new TaskExecutor(this, testLogger);
        }

        private void SetStoreHasNoMetadata() =>
            _storeMock.Setup(s => s.GetMetadataAsync()).ReturnsAsync((StoreMetadata?)null);
        
        private void SetStoreTimestamp(UnixMillisecondTime timestamp) =>
            _storeMock.Setup(s => s.GetMetadataAsync()).ReturnsAsync(new StoreMetadata { LastUpToDate = timestamp });

        private void SetStoreStatusError(Exception e) =>
            _storeMock.Setup(s => s.GetMetadataAsync()).ThrowsAsync(e);

        private void SetStoreMembership(string userKey, IMembership membership) =>
            _storeMock.Setup(s => s.GetMembershipAsync(BigSegmentUserKeyHash(userKey)))
                .ReturnsAsync(membership);

        private void ShouldHaveQueriedMembershipTimes(string userKey, int times) =>
            _storeMock.Verify(s => s.GetMembershipAsync(BigSegmentUserKeyHash(userKey)),
                Times.Exactly(times));

        [Fact]
        public void MembershipQueryWithUncachedResultAndHealthyStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromDays(1))
                .CreateBigSegmentsConfiguration(basicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, _taskExecutor, testLogger))
            {
                var result = sw.GetUserMembership(userKey);
                Assert.Equal(expectedMembership, result.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result.Status);
            }
        }

        [Fact]
        public void MembershipQueryWithCachedResultAndHealthyStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromDays(1))
                .CreateBigSegmentsConfiguration(basicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, _taskExecutor, testLogger))
            {
                var result1 = sw.GetUserMembership(userKey);
                Assert.Equal(expectedMembership, result1.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result1.Status);

                var result2 = sw.GetUserMembership(userKey);
                Assert.Equal(expectedMembership, result2.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result2.Status);

                ShouldHaveQueriedMembershipTimes(userKey, 1);
            }
        }

        [Fact]
        public void MembershipQueryWithStaleStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(-1000));

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromMilliseconds(500))
                .CreateBigSegmentsConfiguration(basicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, _taskExecutor, testLogger))
            {
                var result = sw.GetUserMembership(userKey);
                Assert.Equal(expectedMembership, result.Membership);
                Assert.Equal(BigSegmentsStatus.Stale, result.Status);
            }
        }

        [Fact]
        public void MembershipQueryWithStaleStatusDueToNoStoreMetadata()
        {
            SetStoreHasNoMetadata();

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromMilliseconds(500))
                .CreateBigSegmentsConfiguration(basicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, _taskExecutor, testLogger))
            {
                var result = sw.GetUserMembership(userKey);
                Assert.Equal(expectedMembership, result.Membership);
                Assert.Equal(BigSegmentsStatus.Stale, result.Status);
            }
        }

        [Fact]
        public void LeastRecentUserIsEvictedFromCache()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            const string userKey1 = "userkey1", userKey2 = "userkey2", userKey3 = "userkey3";
            IMembership expectedMembership1 = NewMembershipFromSegmentRefs(new string[] { "seg1" }, null),
                expectedMembership2 = NewMembershipFromSegmentRefs(new string[] { "seg2" }, null),
                expectedMembership3 = NewMembershipFromSegmentRefs(new string[] { "seg3" }, null);
            SetStoreMembership(userKey1, expectedMembership1);
            SetStoreMembership(userKey2, expectedMembership2);
            SetStoreMembership(userKey3, expectedMembership3);

            var bsConfig = Components.BigSegments(_storeFactory)
                .UserCacheSize(2)
                .StaleAfter(TimeSpan.FromDays(1))
                .CreateBigSegmentsConfiguration(basicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, _taskExecutor, testLogger))
            {
                var result1 = sw.GetUserMembership(userKey1);
                Assert.Equal(expectedMembership1, result1.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result1.Status);

                var result2 = sw.GetUserMembership(userKey2);
                Assert.Equal(expectedMembership2, result2.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result2.Status);

                var result3 = sw.GetUserMembership(userKey3);
                Assert.Equal(expectedMembership3, result3.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result3.Status);

                ShouldHaveQueriedMembershipTimes(userKey1, 1);
                ShouldHaveQueriedMembershipTimes(userKey2, 1);
                ShouldHaveQueriedMembershipTimes(userKey3, 1);

                // Since the capacity is only 2 and userKey1 was the least recently used, that key should be
                // evicted by the userKey3 query. Now only userKey2 and userKey3 are in the cache, and
                // querying them again should not cause a new query to the store.

                var result2a = sw.GetUserMembership(userKey2);
                Assert.Equal(expectedMembership2, result2a.Membership);
                var result3a = sw.GetUserMembership(userKey3);
                Assert.Equal(expectedMembership3, result3a.Membership);

                ShouldHaveQueriedMembershipTimes(userKey1, 1);
                ShouldHaveQueriedMembershipTimes(userKey2, 1);
                ShouldHaveQueriedMembershipTimes(userKey3, 1);

                var result1a = sw.GetUserMembership(userKey1);
                Assert.Equal(expectedMembership1, result1a.Membership);

                ShouldHaveQueriedMembershipTimes(userKey1, 2);
                ShouldHaveQueriedMembershipTimes(userKey2, 1);
                ShouldHaveQueriedMembershipTimes(userKey3, 1);
            }
        }

        [Fact]
        public void PollingDetectsStoreUnavailability()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StatusPollInterval(TimeSpan.FromMilliseconds(10))
                .StaleAfter(TimeSpan.FromDays(1))
                .CreateBigSegmentsConfiguration(basicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, _taskExecutor, testLogger))
            {
                var status1 = sw.GetStatus();
                Assert.True(status1.Available);

                var statuses = new EventSink<BigSegmentStoreStatus>();
                sw.StatusChanged += statuses.Add;

                SetStoreStatusError(new Exception("sorry"));

                var status2 = statuses.ExpectValue();
                Assert.False(status2.Available);
                Assert.Equal(status2, sw.GetStatus());

                SetStoreTimestamp(UnixMillisecondTime.Now);

                var status3 = statuses.ExpectValue();
                Assert.True(status3.Available);
                Assert.Equal(status3, sw.GetStatus());
            }

            Assert.True(logCapture.HasMessageWithRegex(Logging.LogLevel.Error,
                "Big segment store status.*Exception.*sorry"));
        }

        [Fact]
        public void PollingDetectsStaleStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(5000)); // future time, definitely not stale

            var bsConfig = Components.BigSegments(_storeFactory)
                .StatusPollInterval(TimeSpan.FromMilliseconds(10))
                .StaleAfter(TimeSpan.FromMilliseconds(200))
                .CreateBigSegmentsConfiguration(basicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, _taskExecutor, testLogger))
            {
                var status1 = sw.GetStatus();
                Assert.False(status1.Stale);

                var statuses = new EventSink<BigSegmentStoreStatus>();
                sw.StatusChanged += statuses.Add;

                SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(-200));

                var status2 = statuses.ExpectValue();
                Assert.True(status2.Stale);

                SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(5000));

                var status3 = statuses.ExpectValue();
                Assert.False(status3.Stale);
            }
        }
    }
}
