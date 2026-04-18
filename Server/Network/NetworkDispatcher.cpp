/*
 * Created by StarFlame on 2026/2/28.
 * 网络消息分发
 */

#define FILE_PREFIX "Dispatcher:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Info//局部日志等级

#include "NetworkDispatcher.h"
#include "../LoggerStream.h"

NetworkDispatcher::NetworkDispatcher(QObject *parent)
    :QObject(parent),m_tcpServer(this),m_udpServer(this)
{
    startTime=QDateTime::currentMSecsSinceEpoch();
    connect(&m_tcpServer,&TcpServer::addNewClient,this,&NetworkDispatcher::addClient);//客户端id分配
    connect(&m_tcpServer,&TcpServer::deleteClient,this,&NetworkDispatcher::deleteClient);//移除客户端

    connect(&m_tcpServer,&TcpServer::receiveMessage,this,&NetworkDispatcher::handleTcpMessage);
    connect(&m_udpServer,&UdpServer::receiveMessage,this,&NetworkDispatcher::handleUdpMessage);

}

NetworkDispatcher::~NetworkDispatcher()
{
    quint64 endTime=QDateTime::currentMSecsSinceEpoch();
    //qDebug()<<"服务器运行时处理包:"<<messageNum;
}

void NetworkDispatcher::sendTcpMessageDirect(QTcpSocket *socket,const QByteArray &message)
{
    m_tcpServer.sendMessage(socket,message);
}

void NetworkDispatcher::sendTcpMessage(quint64 clientId, const QByteArray& message)
{
    if (m_clientsMap.contains(clientId))
    {
        sendTcpMessageDirect(m_clientsMap[clientId].socket,message);

    }else Log_Error()<<"[sendTcpMessage]未找到客户端Id"<<clientId;
}

void NetworkDispatcher::sendUdpMessageDirect(const QHostAddress& address, quint16 port, const QByteArray& message)
{
    m_udpServer.sendMessage(address,port,message);
}

void NetworkDispatcher::sendUdpMessage(quint64 clientId, const QByteArray& message)
{
    if (m_clientsMap.contains(clientId))
    {
        Client &client=m_clientsMap[clientId];
        sendUdpMessageDirect(client.udpEndPoint.address,client.udpEndPoint.port,message);

    }else Log_Error()<<"[sendUdpMessage]未找到客户端Id"<<clientId;
}

void NetworkDispatcher::handleTcpMessage(QTcpSocket* socket, const QByteArray& message)
{
    using namespace SyncMessage;
    messageNum++;
    ClientMessage clientMessage;
    clientMessage.ParseFromArray(message,message.size());
    quint64 clientId=clientMessage.clientid();
    checkClient(clientId,socket);
    switch (clientMessage.content_case())
    {
        case ClientMessage::kCommonMessage:
            Log_Info()<<"[handleTcpMessage][Tcp:"<<m_tcpServer.getTcpSocketInfo(socket)<<"-"<<clientId<<"]"<<clientMessage.commonmessage();
            break;
        case ClientMessage::kConnectMessage:
            Log_Error()<<"kConnectMessage被删除";
            break;
        case ClientMessage::kGameSyncMessage:
            Log_Error()<<"Tcp-kGameSyncMessage被删除";
            break;
        case ClientMessage::kLobbySync:
            Log_Info()<<"[handleTcpMessage][Tcp:"<<m_tcpServer.getTcpSocketInfo(socket)<<"-"<<clientId<<"]kLobbySync";
            emit handleTcpLobby(clientId,clientMessage.lobbysync());
            break;
        default:
            Log_Error()<<"[handleTcpMessage]未知类型:"<<clientMessage.content_case();
            break;
    }
}

void NetworkDispatcher::handleUdpMessage(const QHostAddress& address, const quint16& port,const QByteArray& message)
{
    using namespace SyncMessage;
    messageNum++;
    ClientMessage clientMessage;
    clientMessage.ParseFromArray(message,message.size());
    quint64 clientId=clientMessage.clientid();
    checkClient(clientId,address,port);
    switch (clientMessage.content_case())
    {
        case ClientMessage::kCommonMessage:
            Log_Info()<<"[handleUdpMessage][Udp:"<<m_udpServer.getPeerAddressInfo(address,port)<<"-"<<clientId<<"]"<<clientMessage.commonmessage();
            break;
        case ClientMessage::kConnectMessage:
            Log_Error()<<"kConnectMessage被删除";
            break;
        case ClientMessage::kGameSyncMessage:
            emit handleUdpGameSync(clientId,clientMessage.gamesyncmessage());
            break;
        case ClientMessage::kHeartBeat:
            emit handleUdpHeartBeat(clientId,clientMessage.heartbeat());
            break;
        default:
            Log_Error()<<"[handleUdpMessage]未知类型:"<<clientMessage.content_case();
            break;
    }
}

void NetworkDispatcher::checkClient(qint64 clientId, QTcpSocket* socket)
{
    if (m_clientsMap.contains(clientId))
    {
        bindClient(clientId,socket);
    }
    else
    {
        Log_Error()<<"[checkClient]未知客户端id:[Tcp:"<<m_tcpServer.getTcpSocketInfo(socket)<<"-"<<clientId<<"]";
    }
}

void NetworkDispatcher::checkClient(qint64 clientId, const QHostAddress& address, quint16 port)
{
    if (m_clientsMap.contains(clientId))
    {
        bindClient(clientId,UdpEndPoint(address,port));
    }
    else
    {
        Log_Error()<<"[checkClient]未知客户端id:[Udp:"<<m_udpServer.getPeerAddressInfo(address,port)<<"-"<<clientId<<"]";
    }
}

Client NetworkDispatcher::findClient(qint64 clientId)
{
    return m_clientsMap[clientId];
}

void NetworkDispatcher::addClient(QTcpSocket* socket)
{
    quint64 clientId;
    clientId=nextClientId;
    Client client;
    client.clientId=clientId;
    nextClientId++;
    m_clientsMap.insert(clientId,client);


    using namespace ConnectMessage;
    using namespace SyncMessage;
    ServerMessage message;
    auto *connectMessage= message.mutable_connectmessage();
    auto *response=connectMessage->mutable_handshakemessage();
    response->set_content("Tcp-这里是服务器,建立连接");
    response->set_clientid(clientId);
    QByteArray data;
    data.resize(message.ByteSizeLong());
    message.SerializeToArray(data.data(),data.size());

    sendTcpMessageDirect(socket,data);


    Log_Info()<<"分配id"<<clientId<<":"<<m_tcpServer.getTcpSocketInfo(socket);
}

void NetworkDispatcher::deleteClient(QTcpSocket* socket)
{
    if (m_tcpClientsMap.contains(socket))
    {
        quint64 clientId=m_tcpClientsMap[socket];
        Client &c=m_clientsMap[clientId];
        Log_Info()<<"移除"<<m_tcpServer.getTcpSocketInfo(socket)<<"客户端id"<<c.clientId;
        emit handleClientDelete(c.clientId);

        UdpEndPoint udp= c.udpEndPoint;
        m_udpServer.cleanClient(udp.address,udp.port);
        m_clientsMap.remove(clientId);
        m_tcpClientsMap.remove(socket);
        m_udpClientsMap.remove(udp);
    }
}

void NetworkDispatcher::bindClient(const quint64 clientId, QTcpSocket* tcpSocket)
{
    if(m_clientsMap.contains(clientId)&&m_tcpClientsMap[tcpSocket]!=clientId)
    {
        m_clientsMap[clientId].socket=tcpSocket;//修改数据本体
        m_tcpClientsMap[tcpSocket]=clientId;
        Log_Debug()<<"[bindClient]绑定客户端id:[Tcp:"<<m_tcpServer.getTcpSocketInfo(tcpSocket)<<"-"<<clientId<<"]";
    }
}

void NetworkDispatcher::bindClient(const quint64 clientId, const UdpEndPoint& udpEndPoint)
{
    if(m_clientsMap.contains(clientId)&&m_udpClientsMap[udpEndPoint]!=clientId)
    {
        m_clientsMap[clientId].udpEndPoint=udpEndPoint;
        m_udpClientsMap[udpEndPoint]=clientId;
        Log_Debug()<<"[bindClient]绑定客户端id:[Udp:"<<m_udpServer.getPeerAddressInfo(udpEndPoint)<<"-"<<clientId<<"]";
    }
}
