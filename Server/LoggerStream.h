//
// Created by StarFlame on 2026/2/18.
//

#ifndef SERVER_LOGGER_H
#define SERVER_LOGGER_H
#include <QString>
#include <QTextStream>


#define ALLOW_LOG_LEVEL LogLevel::Debug//全局日志显示等级

#ifndef LOCAL_LOG_LEVEL
#define LOCAL_LOG_LEVEL ALLOW_LOG_LEVEL//未定义局部日志等级时采用全局日志等级
#endif


enum LogLevel
{
    Debug,Info,Warning,Error
};

class LoggerStream
{
    public:
    explicit LoggerStream(LogLevel level=Debug,LogLevel localLogLevel=Debug);
    ~LoggerStream();

    LoggerStream& operator<<(const std::string& val) {
        m_stream << QString::fromStdString(val);
        return *this;
    }

    template <typename T>
    LoggerStream& operator<<(const T& val)
    {
        m_stream << val;
        return *this;
    }

    private:
    QString m_buf;
    QTextStream m_stream;
    LogLevel m_level;
    LogLevel m_localLogLevel;
};

/*
 * 日志输出前缀实现 前缀必须可以传给qDebug()
 * FILE_PREFIX宏必须比引用此头先定义 否则无效
 */
#ifndef FILE_PREFIX
#define LoggerStreamWithPrefix(level,localLogLevel) LoggerStream(level,localLogLevel)//无前缀
#else
#define LoggerStreamWithPrefix(level,localLogLevel) LoggerStream(level,localLogLevel)<<FILE_PREFIX//有前缀
#endif

#define Debug() LoggerStreamWithPrefix(LogLevel::Debug,LOCAL_LOG_LEVEL)
#define Info() LoggerStreamWithPrefix(LogLevel::Info,LOCAL_LOG_LEVEL)
#define Warning() LoggerStreamWithPrefix(LogLevel::Warning,LOCAL_LOG_LEVEL)
#define Error() LoggerStreamWithPrefix(LogLevel::Error,LOCAL_LOG_LEVEL)

#endif //SERVER_LOGGER_H
