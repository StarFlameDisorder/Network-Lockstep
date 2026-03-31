/*
* Created by StarFlame on 2026/3/5.
 * UDPSocket通信
 */

#define FILE_PREFIX "UdpServer:"
#define LOCAL_LOG_LEVEL LogLevel::Error//局部日志等级

#include "UDPServer.h"
#include <QNetworkDatagram>
#include "../LoggerStream.h"
#include "NetworkDispatcher.h"

UdpServer::UdpServer(QObject* parent)
    :QObject(parent)
{
    Log_Info()<<"初始化UDP服务器 端口："<<1976;
    m_socket=new QUdpSocket(this);

    connect(m_socket,&QUdpSocket::readyRead,this,&UdpServer::receiveSocketMessage);
    m_socket->bind(QHostAddress::Any,1976);
}

void UdpServer::receiveSocketMessage()
{
    Log_Debug()<<"收到消息";
    while (m_socket->hasPendingDatagrams())
    {
        QHostAddress addr;
        quint16 port;
        int length=m_socket->pendingDatagramSize();
        QByteArray message;
        message.resize(length);

        m_socket->readDatagram(message.data(),length,&addr,&port);

        Log_Debug() <<"接受-长度:"<<length<< "原始有效字节:" << message.toHex();//有效载荷长度
        if (length>0)emit receiveMessage(addr,port,message);
        else
        {
            Log_Warning()<<"接收到空包:"<<getPeerAddressInfo(addr,port);
            Log_Warning()<<m_socket->errorString();
        }
    }
}

void UdpServer::sendMessage(const QHostAddress& address, const quint16& port,const QByteArray& message)
{
    qint32 originalLen = message.length();//转成32位
    Log_Debug() <<"发送"<<getPeerAddressInfo(address,port)<<"-长度:"<<originalLen<< "原始有效字节:" << message.toHex();//有效载荷长度
    m_socket->writeDatagram(message,address,port);
}

//获取字符串形式的ip+端口
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

//获取字符串形式的ip+端口
std::string UdpServer::getPeerAddressInfo(const UdpEndPoint& udpEndPoint) const
{
    return  getPeerAddressInfo(udpEndPoint.address,udpEndPoint.port);
}


