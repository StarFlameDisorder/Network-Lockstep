/*
* Created by StarFlame on 2026/3/5.
 * UDPSocket通信
 */

#define FILE_PREFIX "UdpServer:"
#define LOCAL_LOG_LEVEL LogLevel::Info//局部日志等级

#define DELAY 1000
#include "UDPServer.h"
#include <QNetworkDatagram>
#include <QTimer>

#include "../LoggerStream.h"
#include "NetworkDispatcher.h"
#include "QtEndian"

UdpServer::UdpServer(QObject* parent)
    :QObject(parent)
{
    Log_Info()<<"初始化UDP服务器 端口："<<1975;
    m_socket=new QUdpSocket(this);

    connect(m_socket,&QUdpSocket::readyRead,this,&UdpServer::receiveSocketMessage);
    m_socket->bind(QHostAddress::Any,1975);
    connect(&m_timer,&QTimer::timeout,this,&UdpServer::checkAndResend);
    m_timer.start(((float)1000)/2);
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

        if (length<=0)
        {
            Log_Warning()<<"接收到空包:"<<getPeerAddressInfo(addr,port);
            Log_Warning()<<getPeerAddressInfo(addr,port)<<"-"<<m_socket->errorString();
            return;
        }

        QString header=QString::fromUtf8(message.mid(0,3));
        qint64 index=qFromBigEndian<qint64>(reinterpret_cast<const char*>(message.mid(3,8).constData()));
        Log_Debug() <<getPeerAddressInfo(addr,port)<<"-"<<"接受-序号:"<<index<<"-Socket长度:"<<length<<"-总字节:" << message.toHex();

        UdpEndPoint udpEndPoint(addr,port);
        if (header=="SEQ")
        {
            int dataLength=qFromBigEndian<int>(reinterpret_cast<const char*>(message.mid(11,4).constData()));
            QByteArray array=message.mid(15,dataLength);
            Log_Debug() <<getPeerAddressInfo(addr,port)<<"-"<<"接受-长度:"<<length<< "原始有效字节(十六进制):" << array.toHex();//有效载荷长度
            sendACKMessage(addr,port,index);

            //重复判断 排序
            auto &receiveBuf=m_receiveBuf[udpEndPoint];
            auto &invokeIndex=m_invokeIndex[udpEndPoint];
            if (index>=invokeIndex)
            {
                if (!receiveBuf.contains(index))receiveBuf.insert(index,std::move(array));
                else Log_Warning()<<getPeerAddressInfo(addr,port)<<"-"<<"重复包" << index<<"最新已接收包"<<invokeIndex;
            }else Log_Warning()<<getPeerAddressInfo(addr,port)<<"-"<<"接收到旧包" << index<<"最新已接收包"<<invokeIndex;

            while (receiveBuf.contains(invokeIndex))
            {
                emit receiveMessage(addr,port,receiveBuf[invokeIndex]);//潜在跨线程注意点
                receiveBuf.remove(invokeIndex);
                invokeIndex++;
            }
            while (receiveBuf.count()>600)
            {
                Log_Warning()<<getPeerAddressInfo(addr,port)<<"-"<<"UDP缓冲区包过多"<<receiveBuf.count();
                receiveBuf.remove(invokeIndex);
                invokeIndex++;
            }

        }else
        {
            if (header=="ACK")
            {
                if (m_pendingPackets[udpEndPoint].contains(index))
                {
                    if (!m_pendingPackets[udpEndPoint][index].isAck)
                    {
                        Log_Debug()<<getPeerAddressInfo(addr,port)<<"-"<<"接收-ACK序号"<<index;
                        m_pendingPackets[udpEndPoint][index].isAck=true;
                    }
                    else
                    {
                        Log_Warning()<<getPeerAddressInfo(addr,port)<<"-"<<"接收-已确认ACK序号"<<index;
                    }
                }else
                {
                    Log_Warning()<<getPeerAddressInfo(addr,port)<<"-"<<"接收-已舍弃ACK序号"<<index;
                }
            }else Log_Error()<<getPeerAddressInfo(addr,port)<<"-"<<"接收未知类型"+header;
        }
    }
}

void UdpServer::sendACKMessage(const QHostAddress& address, const quint16& port, qint64 index)
{
    QString header="ACK";
    QByteArray sendBuffer;
    qint64 indexheader=qToBigEndian(index);//序号
    sendBuffer.append(header.toUtf8());
    sendBuffer.append(reinterpret_cast<const char*>(&indexheader), sizeof(indexheader));

    Log_Debug()<<"发送ACK"<<getPeerAddressInfo(address,port)<<"-序号"<<index<<"-Socket长度:"<<sendBuffer.size()<<"-总字节:" << sendBuffer.toHex();
    Log_Debug()<<"发送ACK-序号"<<index<<" "<<getPeerAddressInfo(address,port);

    m_socket->writeDatagram(sendBuffer,address,port);
}

void UdpServer::sendMessage(const QHostAddress& address, const quint16& port,const QByteArray& message)
{
    UdpEndPoint endPoint(address,port);
    if (!m_udpIndex.contains(endPoint))
    {
        m_udpIndex.insert(endPoint,0);
    }

    qint32 originalLen = message.length();//转成32位

    QString header="SEQ";
    QByteArray sendBuffer;
    sendBuffer.append(header.toUtf8());//类型

    qint64 index=qToBigEndian(m_udpIndex[endPoint]);//序号
    sendBuffer.append(reinterpret_cast<const char*>(&index), sizeof(index));

    qint32 networkLen=qToBigEndian(originalLen);//长度
    sendBuffer.append(reinterpret_cast<const char*>(&networkLen), sizeof(networkLen));

    sendBuffer.append(message);

    //getPeerAddressInfo(address,port)//信息获取
    Log_Debug()<<"发送"<<"-长度:"<<originalLen<< "原始有效字节:" << message.toHex();//有效载荷长度
    Log_Debug()<<"发送"<<"-序号"<<m_udpIndex[endPoint]<<"-Socket长度:"<<sendBuffer.size()<<"-总字节:" << sendBuffer.toHex();

    m_socket->writeDatagram(sendBuffer,address,port);

    qint64 sendIndex=m_udpIndex[endPoint];
    m_pendingPackets[endPoint][sendIndex]=PendingPacket{
        sendIndex,std::move(sendBuffer),QDateTime::currentMSecsSinceEpoch(),0,false //移动构造
    };
    //m_sendQueue[endPoint].enqueue(sendIndex);

    m_udpIndex[endPoint]++;
    //checkAndResend();
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

void UdpServer::checkAndResend()//发送消息后会检查旧数据是否发送
{
    for (auto &udpEndPoint:m_pendingPackets.keys())
    {
        auto &pair=m_pendingPackets[udpEndPoint];
        qint64 time=QDateTime::currentMSecsSinceEpoch();
        for (auto it=pair.begin();it!=pair.end();)
        {
            qint64 index=it.key();
            PendingPacket& packet = it.value();

            if (packet.isAck)//已应答
            {
                it=pair.erase(it);
                continue;
            }

            if (time-packet.previousTime<DELAY*(1<<(packet.times)))//指数退避
            {
                ++it;
                continue;
            }

            if (packet.times>3)
            {
                Log_Warning()<<getPeerAddressInfo(udpEndPoint)<<"-"<<"[checkAndResend]重传3次失败，序号:"<<index;
                // NACK
                it=pair.erase(it);
            }else
            {
                packet.previousTime=time;
                m_socket->writeDatagram(packet.sendData,udpEndPoint.address,udpEndPoint.port);
                Log_Warning()<<getPeerAddressInfo(udpEndPoint)<<"-"<<"[checkAndResend]重传,延迟"<<DELAY*(1<<(packet.times))<<"序号:"<<index;
                packet.times++;
                ++it;
            }
        }
    }
}

void UdpServer::cleanClient(const QHostAddress& address,const quint16 &port)
{
    Log_Info()<<"断开与"<<getPeerAddressInfo(address,port)<<"的连接";
    UdpEndPoint endPoint(address,port);
    m_udpIndex.remove(endPoint);
    m_pendingPackets.remove(endPoint);
    m_receiveBuf.remove(endPoint);
    m_invokeIndex.remove(endPoint);
}
