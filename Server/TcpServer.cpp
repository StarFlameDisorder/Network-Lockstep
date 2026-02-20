//
// Created by StarFlame on 2026/2/14.
//

#include "TcpServer.h"
#include <QTcpSocket>
#include "LoggerStream.h"

TcpServer::TcpServer(QObject* parent):QTcpServer(parent)
{
    Info()<<"初始化TCP服务器";
    connect(this,&QTcpServer::newConnection,this,&TcpServer::tcpServerConnectionNew);
    listen(QHostAddress::Any, 1975);
}

void TcpServer::SendMessageById(quint64 id, QString message)
{
    QTcpSocket *tcpSocket=m_idTcpSocketMap.value(id);
    tcpSocket->write(message.toUtf8());
}

void TcpServer::SendMessageBySocket(QTcpSocket* socket, QString message)
{
    socket->write(message.toUtf8());
}

void TcpServer::tcpServerConnectionNew()
{
    QTcpSocket *newTcpSocket=nextPendingConnection();
    quint64 id=m_tcpNextId;
    m_tcpNextId++;
    Info()<<"新连接:"<<id<<"-"<<getTcpSocketInfo(newTcpSocket);

    m_idTcpSocketMap.insert(id,newTcpSocket);

    newTcpSocket->write(QString("这里是服务器,建立连接").toUtf8());


    connect(newTcpSocket,&QTcpSocket::readyRead,this,[this,newTcpSocket]()
    {
        QByteArray data=newTcpSocket->readAll();
        QByteArray head=data.left(4);
        QByteArray message=data.mid(4);
        Info()<<QString::fromUtf8(message);
        newTcpSocket->write(message+QString("回传").toUtf8());
    });
    connect(newTcpSocket,&QTcpSocket::disconnected,this,[this,newTcpSocket,id]()
    {
        Info()<<"断开连接:"<<id<<"-"<<getTcpSocketInfo(newTcpSocket);
        newTcpSocket->deleteLater();
        m_idTcpSocketMap.remove(id);
    });
}

void TcpServer::tcpServerConnectClosed()
{
    Info()<<"连接断开";

}

/**
 * @brief 获取此QTcpSocket的ip和端口
 * @return
 */
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






