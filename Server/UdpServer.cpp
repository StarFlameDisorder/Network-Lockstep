//
// Created by StarFlame on 2026/3/5.
//

#include "UDPServer.h"
#include <QNetworkDatagram>
#include "LoggerStream.h"

UdpServer::UdpServer(QObject* parent)
{
    Info()<<"UdpServer::初始化UDP服务器 端口："<<1976;
    m_socket=new QUdpSocket(this);

    connect(m_socket,&QUdpSocket::readyRead,this,&UdpServer::receiveSocketMessage);
    m_socket->bind(QHostAddress::Any,1976);
}

void UdpServer::receiveSocketMessage()
{
    Info()<<"UDP:收到消息";
    while (m_socket->hasPendingDatagrams())
    {
        char buf[512];
        QHostAddress addr;
        quint16 port;
        m_socket->readDatagram(buf,512,&addr,&port);
        Info()<<"UDP:"<<getPeerAddressInfo(addr,port)<<QString::fromUtf8(buf);
    }
}

std::string UdpServer::getPeerAddressInfo(const QHostAddress& address, const quint16& port) const
{
    QString out=address.toString();
    if (address.protocol() == QAbstractSocket::IPv6Protocol&&out.startsWith("::ffff:"))
    {
        out.replace("::ffff:","");
    }
    out+=":"+QString::number(port);
    return out.toStdString();
}

