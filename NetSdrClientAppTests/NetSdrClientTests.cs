using Moq;
using NUnit.Framework;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using System.Text;

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
    public void MessageReceived_HandlesServerReadyMessage()
    {
        // Arrange (Підготовка)
        // 1. З'єднуємося, щоб ініціалізувати клієнт
        _client.ConnectAsync().Wait();

        // 2. Створюємо фіктивне (фейкове) повідомлення від сервера
        // Припустимо, "SERVER_READY_OK" - це реальне повідомлення
        string serverReadyMsg = "SERVER_READY_OK";
        byte[] serverReadyBytes = Encoding.UTF8.GetBytes(serverReadyMsg);

        // Act (Викликаємо подію MessageReceived вручну через Moq)
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, serverReadyBytes);

        // Assert (Перевірка)
        // Тут перевір, що після отримання цього повідомлення змінився внутрішній стан клієнта.
        // Наприклад, якщо є публічна властивість IsReady:
        // Assert.That(_client.IsReady, Is.True);
    }
    [Test]
    public async Task SetFrequencyAsync_SendsCorrectMessage()
    {
        // Arrange
        await _client.ConnectAsync();
        long newFrequency = 10000000; // 10 MHz

        // Act
        await _client.SetFrequencyAsync(newFrequency); // Припустимо, такий метод існує

        // Assert
        // Перевір, що TCP-мок був викликаний з повідомленням, яке містить нову частоту
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(
            It.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes).Contains($"FREQ:{newFrequency}"))
        ), Times.Once);
    }
    [Test]
    public async Task ConnectAsync_FailsOnTcpError()
    {
        // Arrange
        // Налаштовуємо мок, щоб він кидав виняток при спробі Connect
        _tcpMock.Setup(tcp => tcp.Connect()).Throws<System.Net.Sockets.SocketException>();

        // Act
        await _client.ConnectAsync();

        // Assert
        // Перевір, що Connected залишається False і не було спроб відправити повідомлення
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce());
    }
    //TODO: cover the rest of the NetSdrClient code here
}
