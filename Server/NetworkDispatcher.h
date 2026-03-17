/*
 * Created by StarFlame on 2026/2/28.
 * 网络模块：消息分发处理
 */

#ifndef SERVER_NETWORKDISPATCHER_H
#define SERVER_NETWORKDISPATCHER_H

#include "Client.h"
#include "TcpServer.h"
#include "UDPServer.h"
#include "protobuf/output/SyncMessage.pb.h"
#include "protobuf/output/ConnectMessage.pb.h"

using namespace SyncMessage;
using namespace ConnectMessage;

class NetworkDispatcher
{
public:
    NetworkDispatcher();
    void sendTcpMessage(QTcpSocket *socket,const QByteArray &message);
    void sendUdpMessage(QHostAddress address,quint16 port,const QByteArray &message);

    //消息处理Tcp
    void handleTcpMessage(QTcpSocket *socket,const QByteArray &message);
    void handleTcpConnection(QTcpSocket *socket,const ClientConnectMessage &message);
    //void handleMessage(quint64 clientId,const ClientMessage &message);

    //消息处理Udp
    //void handleUdpMessage(QHostAddress address,quint16 port,const QByteArray &message);


    //消息-绑定id


    //客户端相关
    QSharedPointer<Client> findClient(qint64 clientId);
    quint64 addClient();
    void bindClient(const quint64 clientId,QTcpSocket *tcpSocket);
    void bindClient(const quint64 clientId, const UdpEndPoint& udpEndPoint);

private:
    TcpServer m_tcpServer;
    UdpServer m_udpServer;
    QHash<quint64,QSharedPointer<Client>> m_clientsMap;
    QHash<QTcpSocket*,quint64> m_tcpClientsMap;
    QHash<UdpEndPoint,quint64> m_udpClientsMap;
    quint64 nextClientId=0;
};


#endif //SERVER_NETWORKDISPATCHER_H