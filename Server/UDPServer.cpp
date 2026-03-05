//
// Created by StarFlame on 2026/3/5.
//

#include "UDPServer.h"
#include <QNetworkDatagram>
#include "LoggerStream.h"

UDPServer::UDPServer(QObject* parent)
{
    m_socket=new QUdpSocket(this);
    m_socket->bind(QHostAddress::Any,1976);

    connect(m_socket,&QUdpSocket::readyRead,this,&UDPServer::receiveSocketMessage);


}

void UDPServer::receiveSocketMessage()
{
    while (m_socket->hasPendingDatagrams())
    {
        char buf[512];
        QHostAddress addr;
        quint16 port;
        m_socket->readDatagram(buf,512,&addr,&port);
        Info()<<getPeerAddressInfo(addr,port)<<QString::fromUtf8(buf);
    }
}

std::string UDPServer::getPeerAddressInfo(const QHostAddress& address, const quint16& port) const
{
    QString out=address.toString();
    if (address.protocol() == QAbstractSocket::IPv6Protocol&&out.startsWith("::ffff:"))
    {
        out.replace("::ffff:","");
    }
    out+=":"+QString::number(port);
    return out.toStdString();
}

