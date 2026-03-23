//
// Created by StarFlame on 2026/2/14.
//

#ifndef SERVER_TCPSERVER_H
#define SERVER_TCPSERVER_H
#include <QTcpServer>
#include <QHash>

class NetworkDispatcher;

class TcpServer:public QTcpServer
{
    Q_OBJECT
public:
    TcpServer(NetworkDispatcher *networkDispatcher,QObject *parent = nullptr);
    std::string getTcpSocketInfo(const QTcpSocket *socket)const;
    void sendMessage(QTcpSocket *socket,QByteArray message);
private:
    void tcpServerConnectionNew();
    void tcpServerConnectClosed();
    QByteArray receiveTcpMessage(QTcpSocket *socket);
    void receiveSocketMessage();
    void receiveMessage(QTcpSocket *socket,QByteArray message);

    quint64 m_tcpNextId=1;
    QHash<QTcpSocket*,QByteArray> m_tcpMessageBuffer;
    NetworkDispatcher *_networkDispatcher;

signals:
    void tcpReadyRead(QTcpSocket* socket,QByteArray data);

};


#endif //SERVER_TCPSERVER_H