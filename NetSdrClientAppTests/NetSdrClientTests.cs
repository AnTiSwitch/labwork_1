using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NUnit.Framework;
using System.Text;
using System.Net.Sockets;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }
    [Test]
    public async Task SetFrequencyAsync_SendsCorrectMessage()
    {
        // Arrange
        await ConnectAsyncTestHelper();
        long newFrequency = 10000000; // 10 MHz

        // МАСИВ БАЙТІВ, ЯКИЙ МАЄ БУТИ ВІДПРАВЛЕНО: [Channel: 0] + [Frequency: 5 байт]
        byte[] expectedBody = BitConverter.GetBytes(newFrequency).Take(5).ToArray();

        // Act
        await _client.SetFrequencyAsync(newFrequency);

        // Assert 
        // Перевіряємо, що було викликано SendTcpRequest з кодом ReceiverFrequency (0x20 або 32)
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(
            It.Is<byte[]>(bytes =>
                // Перевіряємо, що повідомлення містить правильний код
                bytes[2] == 0x20 &&
                // Перевіряємо, що тіло повідомлення містить правильну частоту 
                bytes.Skip(5).Take(expectedBody.Length).SequenceEqual(expectedBody)
            )
        ), Times.Once);
    }
    [Test]
    public async Task ConnectAsync_FailsOnTcpError()
    {
        // Arrange
        _tcpMock.Setup(tcp => tcp.Connect()).Throws<InvalidOperationException>();

        // Act
        await _client.ConnectAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }
    [Test]
    public void MessageReceived_HandlesEmptyMessage()
    {
        // Arrange
        _client.ConnectAsync().Wait();
        byte[] emptyBytes = Array.Empty<byte>();

        // Act
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, emptyBytes);

        // Assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Never);
    }
    //TODO: cover the rest of the NetSdrClient code here
}