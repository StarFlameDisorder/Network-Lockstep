//
// Created by StarFlame on 2026/3/5.
//

#define FILE_PREFIX "UDP:"
#define LOCAL_LOG_LEVEL LogLevel::Error//局部日志等级

#include "UDPServer.h"
#include <QNetworkDatagram>
#include "LoggerStream.h"

UdpServer::UdpServer(NetworkDispatcher *networkDispatcher,QObject* parent)
    :QObject(parent),_networkDispatcher(networkDispatcher)
{
    Log_Info()<<"UdpServer::初始化UDP服务器 端口："<<1976;
    m_socket=new QUdpSocket(this);

    connect(m_socket,&QUdpSocket::readyRead,this,&UdpServer::receiveSocketMessage);
    connect(this,&UdpServer::udpReadyRead,this,&UdpServer::receiveMessage);
    m_socket->bind(QHostAddress::Any,1976);
}

void UdpServer::receiveSocketMessage()
{
    Log_Debug()<<"UDP:收到消息";
    while (m_socket->hasPendingDatagrams())
    {
        QHostAddress addr;
        quint16 port;
        quint64 length=m_socket->pendingDatagramSize();
        QByteArray message;
        message.resize(length);

        m_socket->readDatagram(message.data(),length,&addr,&port);

        Log_Debug() <<"接受-长度:"<<length<< "原始有效字节:" << message.toHex();//有效载荷长度
        emit udpReadyRead(message,addr,port);
    }
}

void UdpServer::sendMessage(const QHostAddress& address, const quint16& port,const QByteArray& message)
{
    qint32 originalLen = message.length();//转成32位
    Log_Debug() <<"发送-长度:"<<originalLen<< "原始有效字节:" << message.toHex();//有效载荷长度
    m_socket->writeDatagram(message,address,port);
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

void UdpServer::receiveMessage(const QByteArray& message, const QHostAddress& address, const quint16& port)
{
    Log_Info()<<getPeerAddressInfo(address,port)<<" "<<QString::fromUtf8(message);
    sendMessage(address,port,message+QString("回传").toUtf8());
}

