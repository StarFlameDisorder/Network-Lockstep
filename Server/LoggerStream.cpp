//
// Created by StarFlame on 2026/2/18.
//

#include "LoggerStream.h"
#include <QTime>

LoggerStream::LoggerStream(LogLevel level,LogLevel localLogLevel)
    :m_stream(&m_buf),m_level(level),m_localLogLevel(localLogLevel)
{
    if(m_level>=m_localLogLevel)
    {
        m_stream<<QTime::currentTime().toString("HH:mm:ss");
        switch (level)
        {
        case LogLevel::Debug:
            m_stream<<" [Debug] ";
            break;
        case LogLevel::Info:
            m_stream<<" [Info] ";
            break;
        case LogLevel::Warning:
            m_stream<<" [Warning] ";
            break;
        case LogLevel::Error:
            m_stream<<" [Error] ";
            break;
        }
    }

}

LoggerStream::~LoggerStream()
{
    if (m_level>=m_localLogLevel)qDebug().noquote()<<m_buf;
}
