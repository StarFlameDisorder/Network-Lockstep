//
// Created by StarFlame on 2026/2/14.
//

#include "TcpServer.h"
#include <QTcpSocket>
#include "LoggerStream.h"

TcpServer::TcpServer(QObject* parent):QTcpServer(parent)
{
    Debug()<<"初始化服务器";
    connect(this,&QTcpServer::newConnection,this,&TcpServer::tcpServerConnectionNew);
    listen(QHostAddress::Any, 1975);
}

void TcpServer::tcpServerConnectionNew()
{
    QTcpSocket *newTcpSocket=nextPendingConnection();
    quint64 id=m_idTcpSocketMap.size();
    Debug()<<"新连接["<<id<<"-"<<getTcpSocketInfo(newTcpSocket)<<"]";

    m_idTcpSocketMap.insert(id,newTcpSocket);

    connect(newTcpSocket,&QTcpSocket::readyRead,this,[this,newTcpSocket]()
    {
        QByteArray data=newTcpSocket->readAll();
        Debug()<<QString::fromUtf8(data);
    });
    connect(newTcpSocket,&QTcpSocket::disconnected,this,[this,newTcpSocket,id]()
    {
        Debug()<<"断开连接:"<<id;
        newTcpSocket->deleteLater();
        m_idTcpSocketMap.remove(id);
    });
}

void TcpServer::tcpServerConnectClosed()
{
    Debug()<<"连接断开";

}

std::string TcpServer::getTcpSocketInfo(const QTcpSocket* socket) const
{
    QHostAddress address = socket->peerAddress();
    QString out=address.toString();
    if (address.protocol() == QAbstractSocket::IPv6Protocol&&out.startsWith("::ffff:"))
    {
        out.replace("::ffff:","");
    }
    out+=":"+QString::number(socket->peerPort());
    return out.toStdString();
}



