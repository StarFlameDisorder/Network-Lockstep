/*
 * Created by StarFlame on 2026/2/28.
 * 网络模块：消息分发处理
 */

#ifndef SERVER_NETWORKDISPATCHER_H
#define SERVER_NETWORKDISPATCHER_H

#include "Client.h"
#include "TcpServer.h"
#include "UDPServer.h"

class NetworkDispatcher
{
public:
    NetworkDispatcher();
    void sendTcpMessage(QTcpSocket *socket,const QByteArray &message);
    void handleTcpMessage(QTcpSocket *socket,const QByteArray &message);

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