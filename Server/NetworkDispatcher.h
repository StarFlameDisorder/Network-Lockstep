/*
 * Created by StarFlame on 2026/2/28.
 * 网络模块：消息分发处理
 */

#ifndef SERVER_NETWORKDISPATCHER_H
#define SERVER_NETWORKDISPATCHER_H


#include "TcpServer.h"
#include "UDPServer.h"
#include <QTime>
#include "protobuf/output/SyncMessage.pb.h"
#include "protobuf/output/ConnectMessage.pb.h"
#include "protobuf/output/GameMessage.pb.h"

using namespace SyncMessage;
using namespace ConnectMessage;
using namespace GameMessage;

struct Client
{
    quint64 clientId;
    QDateTime activeTime;
    QTcpSocket *socket;
    UdpEndPoint udpEndPoint;
};



class NetworkDispatcher
{
public:
    NetworkDispatcher();
    void sendTcpMessage(QTcpSocket *socket,const QByteArray &message);
    void sendUdpMessage(const QHostAddress& address, quint16 port, const QByteArray &message);

    //消息处理Tcp
    void handleTcpMessage(QTcpSocket *socket,const QByteArray &message);
    void handleTcpConnection(QTcpSocket *socket,const ClientConnectMessage &message);
    //void handleMessage(quint64 clientId,const ClientMessage &message);

    //消息处理Udp
    void handleUdpMessage(const QHostAddress &address,quint16 port,const QByteArray &message);
    void handleUdpConnection(const QHostAddress &address,quint16 port,const ClientConnectMessage &message);

    //消息处理
    void handleGameSync(quint64 clientId,const GameSyncMessage& message);


    //客户端相关
    void checkClient(qint64 clientId,QTcpSocket *socket);
    void checkClient(qint64 clientId,const QHostAddress &address, quint16 port);
    Client findClient(qint64 clientId);
    quint64 addClient();
    void bindClient(const quint64 clientId,QTcpSocket *tcpSocket);
    void bindClient(const quint64 clientId, const UdpEndPoint& udpEndPoint);

    //广播
    void broadcastGameSync(const GameSyncMessage& message);

private:
    TcpServer m_tcpServer;
    UdpServer m_udpServer;
    QHash<quint64,Client> m_clientsMap;
    QHash<QTcpSocket*,quint64> m_tcpClientsMap;
    QHash<UdpEndPoint,quint64> m_udpClientsMap;
    quint64 nextClientId=1;
};


#endif //SERVER_NETWORKDISPATCHER_H