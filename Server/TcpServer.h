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
    void sendMessageById(quint64 id,QString message);
    void sendMessageBySocket(QTcpSocket* socket,QString message);
    std::string getTcpSocketInfo(const QTcpSocket *socket)const;
private:
    void tcpServerConnectionNew();
    void tcpServerConnectClosed();
    QByteArray receiveTcpMessage(QTcpSocket *socket);
    void receiveSocketMessage();
    void sendMessage(QTcpSocket *socket,QByteArray message);
    void receiveMessage(QTcpSocket *socket,QByteArray message);

    QHash<quint64,QTcpSocket*> m_tcpIdSocketMap;
    quint64 m_tcpNextId=1;
    qint32 m_messageId=0;
    QHash<QTcpSocket*,QByteArray> m_tcpMessageBuffer;
    NetworkDispatcher *_networkDispatcher;

signals:
    void tcpReadyRead(QTcpSocket* socket,QByteArray data);

};


#endif //SERVER_TCPSERVER_H