using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace heliomaster {
    public struct UploadResult {
        public bool Success;
        public string StatusDescription;
        public Exception Error;
    }

//    public class FTPUploader {
//        private class UploadState {
//            public Stream istream;
//            public Uri uri;
//            public UploadResult result;
//        }
//
//        public string Host;
//        public NetworkCredential Credentials;
//        public int Port;
//        public int BufferSize = 2048;
//
//        public FTPUploader Init(string host, NetworkCredential credentials, int port=-1) {
//            Host = host;
//            Credentials = credentials;
//            Port = port;
//            return this;
//        }
//
//        public Task<UploadResult> Upload(string fname, string path) {
//            Console.WriteLine(fname);
//            return Upload(File.OpenRead(fname), path);
//        }
//
//        public Task<UploadResult> Upload(Stream istream, string path) {
//            return Task<UploadResult>.Factory.StartNew(_s => {
//                var s = (UploadState) _s;
//                try {
//                    Console.WriteLine(s.uri);
//                    var request = (FtpWebRequest) WebRequest.Create(s.uri);
//                    request.Method      = WebRequestMethods.Ftp.UploadFile;
//                    request.Credentials = Credentials;
//                    var ostream = request.GetRequestStream();
//
//                    int readBytes;
//                    var buffer = new byte[BufferSize];
//                    do {
//                        readBytes = s.istream.Read(buffer, 0, BufferSize);
//                        ostream.Write(buffer, 0, readBytes);
//                    } while (readBytes != 0);
//                    ostream.Close();
//                    var response = (FtpWebResponse) request.GetResponse();
//                    response.Close();
//
//                    s.result.StatusDescription = response.StatusDescription;
//                    s.result.Success = response.StatusCode == FtpStatusCode.ClosingData;
//                } catch (Exception e) {
//                    s.result.StatusDescription = e.Message;
//                    s.result.Error = e;
//                }
//                return s.result;
//            }, new UploadState {
//                istream = istream,
//                uri     = new UriBuilder("ftp", Host, Port, path).Uri,
//                result  = new UploadResult()
//            });
//        }
//    }

    public class FTPUploader : IUploader {
        private class UploadState {
            public Stream istream;
            public Uri uri;
            public UploadResult result;
        }

        public string Host;
        public NetworkCredential Credentials;
        public int Port;
        public int BufferSize = 2048;

        public FTPUploader Init(string host, NetworkCredential credentials, int port=-1) {
            Host = host;
            Credentials = credentials;
            Port = port;
            return this;
        }

        public Task Upload(string fname, string path) {
            Console.WriteLine(fname);
            return Upload(File.OpenRead(fname), path);
        }

        public Task Upload(Stream istream, string path) {
            return Task<UploadResult>.Factory.StartNew(_s => {
                var s = (UploadState) _s;
                try {
                    Console.WriteLine(s.uri);
                    var request = (FtpWebRequest) WebRequest.Create(s.uri);
                    request.Method      = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = Credentials;
                    var ostream = request.GetRequestStream();

                    int readBytes;
                    var buffer = new byte[BufferSize];
                    do {
                        readBytes = s.istream.Read(buffer, 0, BufferSize);
                        ostream.Write(buffer, 0, readBytes);
                    } while (readBytes != 0);
                    ostream.Close();
                    var response = (FtpWebResponse) request.GetResponse();
                    response.Close();

                    s.result.StatusDescription = response.StatusDescription;
                    s.result.Success = response.StatusCode == FtpStatusCode.ClosingData;
                } catch (Exception e) {
                    s.result.StatusDescription = e.Message;
                    s.result.Error = e;
                }
                return s.result;
            }, new UploadState {
                istream = istream,
                uri     = new UriBuilder("ftp", Host, Port, path).Uri,
                result  = new UploadResult()
            });
        }
    }
}
