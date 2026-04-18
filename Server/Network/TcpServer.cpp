/*
* Created by StarFlame on 2026/2/14.
 * TcpSocket通信
 */

#define FILE_PREFIX "TcpServer:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Info//局部日志等级

#include "TcpServer.h"
#include <QTcpSocket>
#include "../LoggerStream.h"
#include "../protobuf/output/SyncMessage.pb.h"
#include "../protobuf/output/ConnectMessage.pb.h"
#include "QtEndian"

TcpServer::TcpServer(QObject* parent):QTcpServer(parent)
{
    Log_Info()<<"初始化TCP服务器 端口："<<1975;
    connect(this,&QTcpServer::newConnection,this,&TcpServer::tcpServerConnectionNew);
    connect(this,&TcpServer::tcpReadyRead,this,&TcpServer::receiveMessage);
    listen(QHostAddress::Any, 1975);
}

void TcpServer::tcpServerConnectionNew()
{
    QTcpSocket *newTcpSocket=nextPendingConnection();
    Log_Info()<<"新连接:"<<getTcpSocketInfo(newTcpSocket);

    m_tcpMessageBuffer.insert(newTcpSocket,QByteArray());
    

    connect(newTcpSocket,&QTcpSocket::readyRead,this,&TcpServer::receiveSocketMessage);

    connect(newTcpSocket,&QTcpSocket::disconnected,this,[this,newTcpSocket]()
    {
        emit clientDisconnectRequest(newTcpSocket);
    });

    emit addNewClient(newTcpSocket);
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

QByteArray TcpServer::receiveTcpMessage(QTcpSocket* socket)
{
    QByteArray data=socket->readAll();
    QByteArray head=data.left(4);
    int length=qFromBigEndian<int>(reinterpret_cast<const char*>(head.constData()));
    //Info()<<length;

    Log_Debug() <<"接受-长度:"<<length<< "原始字节:" << data.toHex();
    return data.mid(4);
}

/**
 * @brief 获取此QTcpSocket收到的信息并缓存 使用信号与槽
 */
void TcpServer::receiveSocketMessage()
{
    QTcpSocket *socket=qobject_cast<QTcpSocket*>(sender());
    if (!socket)return;

    QByteArray &messageBuffer=m_tcpMessageBuffer[socket];
    QByteArray buf=socket->readAll();
    messageBuffer.append(buf);//缓冲区大量移动 潜在优化成环形缓冲区

    Log_Debug() <<"接受-Socket长度:"<<buf.length();
    while (true)
    {
        if (messageBuffer.size()<4)break;
        //获取信息的有效部分的字节长度 避免分包黏包
        int length=qFromBigEndian<int>(reinterpret_cast<const char*>(messageBuffer.constData()));//以大端序读取长度头
        if (length<=0||length>1024)
        {
            Log_Error()<<"错误有效载荷长度："<<length;
            break;
        }
        if (messageBuffer.size()<length+4)break;
        QByteArray message=messageBuffer.mid(4,length);
        Log_Debug() <<"接受-长度:"<<length<< "原始有效字节:" << message.toHex();//有效载荷长度
        messageBuffer.remove(0,length+4);

        emit tcpReadyRead(socket,message);
    }
}

void TcpServer::sendMessage(QTcpSocket* socket, QByteArray message)
{
    if (!socket){Log_Error()<<"尝试发送消息给关闭的TcpSocket";return;}
    qint32 originalLen = message.length();//转成32位
    qint32 networkLen=qToBigEndian(originalLen);
    QByteArray send;
    send.append(reinterpret_cast<const char*>(&networkLen), sizeof(networkLen));
    send.append(message);
    Log_Debug() <<"发送-长度:"<<originalLen<< "原始有效字节:" << message.toHex();//有效载荷长度
    socket->write(send);
}

void TcpServer::cleanClient(QTcpSocket* socket)
{
    if (!socket)return;

    Log_Info()<<"断开连接:"<<getTcpSocketInfo(socket);
    m_tcpMessageBuffer.remove(socket);

    disconnect(socket,&QTcpSocket::readyRead,this,&TcpServer::receiveSocketMessage);

    socket->deleteLater();
}






