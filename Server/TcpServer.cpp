//
// Created by StarFlame on 2026/2/14.
//

#include "TcpServer.h"

#include <QtEndian>
#include <QTcpSocket>
#include "LoggerStream.h"

TcpServer::TcpServer(QObject* parent):QTcpServer(parent)
{
    Info()<<"初始化TCP服务器";
    connect(this,&QTcpServer::newConnection,this,&TcpServer::tcpServerConnectionNew);
    listen(QHostAddress::Any, 1975);
}

void TcpServer::sendMessageById(quint64 id, QString message)
{
    QTcpSocket *tcpSocket=m_idTcpSocketMap.value(id);
    tcpSocket->write(message.toUtf8());
}

void TcpServer::sendMessageBySocket(QTcpSocket* socket, QString message)
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

    sendMessage(newTcpSocket,QString("这里是服务器,建立连接").toUtf8());


    connect(newTcpSocket,&QTcpSocket::readyRead,this,[this,newTcpSocket]()
    {
        QByteArray message=receiveMessage(newTcpSocket);
        Info()<<QString::fromUtf8(message);
        sendMessage(newTcpSocket,message+QString("回传").toUtf8());
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

QByteArray TcpServer::receiveMessage(QTcpSocket* socket)
{
    QByteArray data=socket->readAll();
    QByteArray head=data.left(4);
    int length=qFromBigEndian<int>(reinterpret_cast<const char*>(head.constData()));
    //Info()<<length;

    Debug() <<"接受-长度:"<<length<< "原始字节:" << data.toHex();
    return data.mid(4);
}

void TcpServer::sendMessage(QTcpSocket* socket, QByteArray message)
{
    qint32 originalLen = message.length();//转成32位
    qint32 networkLen=qToBigEndian(originalLen);
    QByteArray send;
    send.append(reinterpret_cast<const char*>(&networkLen), sizeof(networkLen));
    send.append(message);
    Debug() <<"发送-长度:"<<originalLen<< "原始字节:" << send.toHex();
    socket->write(send);
}






