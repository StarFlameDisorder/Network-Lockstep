//
// Created by StarFlame on 2026/2/18.
//

#ifndef SERVER_LOGGER_H
#define SERVER_LOGGER_H
#include <QString>
#include <QTextStream>

#define ALLOW_LOG_LEVEL LogLevel::Info

enum LogLevel
{
    Debug,Info,Warning,Error
};

class LoggerStream
{
    public:
    explicit LoggerStream(LogLevel level=Debug);
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
};

/*
 * 日志输出前缀实现 前缀必须可以传给qDebug()
 * FILE_PREFIX宏必须比引用此头先定义 否则无效
 */
#ifndef FILE_PREFIX
#define LoggerStreamWithPrefix(level) LoggerStream(level)//无前缀
#else
#define LoggerStreamWithPrefix(level) LoggerStream(level)<<FILE_PREFIX//有前缀
#endif

#define Debug() LoggerStreamWithPrefix(LogLevel::Debug)
#define Info() LoggerStreamWithPrefix(LogLevel::Info)
#define Warning() LoggerStreamWithPrefix(LogLevel::Warning)
#define Error() LoggerStreamWithPrefix(LogLevel::Error)

#endif //SERVER_LOGGER_H
