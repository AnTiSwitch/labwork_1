using Moq;
using NUnit.Framework;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using System.Text;
using System.Net.Sockets; // Додано для SocketException

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    // Конструктор класу (залишаємо порожнім для NUnit/xUnit)
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

        // Припускаємо, що клас NetSdrClient приймає 2 інтерфейси
        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    // Допоміжний метод для підключення
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }


    [Test]
    public async Task ConnectAsyncVerifySetup()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public void DisconnectWithNoConnectionTest()
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
        _updMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    // НОВИЙ ТЕСТ 1: Обробка повідомлень (вимагає публічної властивості IsReady у NetSdrClient)
    [Test]
    public void MessageReceived_HandlesServerReadyMessage()
    {
        // Arrange (Підготовка)
        _client.ConnectAsync().Wait();
        string serverReadyMsg = "SERVER_READY_OK";
        byte[] serverReadyBytes = Encoding.UTF8.GetBytes(serverReadyMsg);

        // Act (Викликаємо подію MessageReceived вручну через Moq)
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, serverReadyBytes);

        // Assert (Перевірка)
        // ВИПРАВ ТУТ, ЯКЩО У NetSdrClient НЕМАЄ ВЛАСТИВОСТІ IsReady
        // АБО ДОДАЙТЕ ЇЇ ТУДИ
        // Припускаємо, що після цього повідомлення _client.IsReady стає True
        Assert.That(_client.IsReady, Is.True);
    }

    // НОВИЙ ТЕСТ 2: Відправка команди частоти (вимагає SetFrequencyAsync у NetSdrClient)
    [Test]
    public async Task SetFrequencyAsync_SendsCorrectMessage()
    {
        // Arrange
        await ConnectAsyncTest();
        long newFrequency = 10000000; // 10 MHz
        string expectedSubstring = $"FREQ:{newFrequency}"; // Згідно з логікою, доданою у NetSdrClient.cs

        // Act
        await _client.SetFrequencyAsync(newFrequency);

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(
            It.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes).Contains(expectedSubstring))
        ), Times.Once);
    }

    // НОВИЙ ТЕСТ 3: Обробка помилок
    [Test]
    public async Task ConnectAsync_FailsOnTcpError()
    {
        // Arrange
        // Налаштовуємо мок, щоб він кидав виняток при спробі Connect
        _tcpMock.Setup(tcp => tcp.Connect()).Throws<SocketException>();

        // Act
        // Очікуємо, що метод не кидає виняток і коректно обробляє помилку
        await _client.ConnectAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        // Додаткова перевірка стану, якщо є IsConnected
        // Assert.That(_client.IsConnected, Is.False);
    }
    //TODO: cover the rest of the NetSdrClient code here
}