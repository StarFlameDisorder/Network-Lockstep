//
// Created by StarFlame on 2026/2/14.
//

#ifndef SERVER_TCPSERVER_H
#define SERVER_TCPSERVER_H
#include <QTcpServer>
#include <QHash>

class TcpServer:public QTcpServer
{
public:
    TcpServer(QObject *parent = nullptr);
    void sendMessageById(quint64 id,QString message);
    void sendMessageBySocket(QTcpSocket* socket,QString message);
private:
    void tcpServerConnectionNew();
    void tcpServerConnectClosed();
    std::string getTcpSocketInfo(const QTcpSocket *socket)const;
    QByteArray receiveMessage(QTcpSocket *socket);
    void sendMessage(QTcpSocket *socket,QByteArray message);

    QHash<quint64,QTcpSocket*> m_idTcpSocketMap;
    quint64 m_tcpNextId=0;
};


#endif //SERVER_TCPSERVER_H