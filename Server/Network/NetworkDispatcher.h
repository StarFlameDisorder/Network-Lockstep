/*
 * Created by StarFlame on 2026/2/28.
 * 网络模块：消息分发处理
 */

#ifndef SERVER_NETWORKDISPATCHER_H
#define SERVER_NETWORKDISPATCHER_H


#include <QTime>
#include "TcpServer.h"
#include "UDPServer.h"
#include "../protobuf/output/SyncMessage.pb.h"
#include "../protobuf/output/ConnectMessage.pb.h"
#include "../protobuf/output/GameMessage.pb.h"

// using namespace SyncMessage;
// using namespace ConnectMessage;
// using namespace GameMessage;

struct Client
{
    quint64 clientId;
    QDateTime activeTime;
    QTcpSocket *socket;
    UdpEndPoint udpEndPoint;
};



class NetworkDispatcher:public QObject
{
    Q_OBJECT
public:
    NetworkDispatcher(QObject *parent=nullptr);
    ~NetworkDispatcher();

    void sendTcpMessageDirect(QTcpSocket *socket,const QByteArray &message);
    void sendTcpMessage(quint64 clientId,const QByteArray &message);

    void sendUdpMessageDirect(const QHostAddress& address, quint16 port, const QByteArray &message);
    void sendUdpMessage(quint64 clientId,const QByteArray &message);

    //消息处理Tcp
    void handleTcpMessage(QTcpSocket *socket,const QByteArray &message);
    //void handleTcpConnection(QTcpSocket *socket,const ConnectMessage::ClientConnectMessage &message);
    //void handeleTcpLobby(quint64 clientId,const LobbyMessage::LobbySyncRequest &message);

    //消息处理Udp
    void handleUdpMessage(const QHostAddress& address, const quint16& port,const QByteArray& message);
    //void handleUdpConnection(const QHostAddress &address,const quint16 &port,const ConnectMessage::ClientConnectMessage &message);

    //消息处理
    void handleGameSync(quint64 clientId,const GameMessage::GameSyncMessage& message);


    //客户端相关
    void checkClient(qint64 clientId,QTcpSocket *socket);
    void checkClient(qint64 clientId,const QHostAddress &address, quint16 port);
    Client findClient(qint64 clientId);
    void addClient(QTcpSocket* socket);
    void bindClient(const quint64 clientId,QTcpSocket *tcpSocket);
    void bindClient(const quint64 clientId, const UdpEndPoint& udpEndPoint);

    //广播
    void broadcastGameSync(const GameMessage::GameSyncMessage& message,quint64 clientId);
signals:
    void handleUdpGameSync(quint64 clientId,const GameMessage::GameSyncMessage &message);
    void handleUdpGameSnapshot(quint64 clientId,const GameMessage::GameSnapshotMessage &message);//未使用
    void handleTcpLobby(quint64 clientId,const LobbyMessage::LobbySyncRequest &message);

private:
    TcpServer m_tcpServer;
    UdpServer m_udpServer;
    QHash<quint64,Client> m_clientsMap;

    QHash<TcpEndPoint,quint64> m_tcpClientsMap;
    QHash<QTcpSocket *,quint64> m_tcpSocketClientsMap;
    QHash<UdpEndPoint,quint64> m_udpClientsMap;

    quint64 nextClientId=1;

    quint64 messageNum=0;
    quint64 startTime;
};


#endif //SERVER_NETWORKDISPATCHER_H